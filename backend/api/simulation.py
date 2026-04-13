from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from typing import List, Optional
from pydantic import BaseModel
import numpy as np
import logging
from concurrent.futures import ProcessPoolExecutor, as_completed
import multiprocessing as mp
from functools import partial
from backend.database import get_db
from backend.models.models import EmissionSource, Receptor, Meteorology
from backend.models.schemas import SimulationRequest, SimulationResult
from backend.core.gaussian_plume import GaussianPlumeModel

router = APIRouter()
logger = logging.getLogger(__name__)

class SimulationWithWindRequest(BaseModel):
    meteorology_id: int
    source_ids: Optional[List[int]] = None
    receptor_ids: Optional[List[int]] = None
    pollutant_type: Optional[str] = None
    grid_resolution: float = 50.0
    domain_size: float = 5000.0
    wind_direction: float
    wind_speed: float
    receptor_height: float = 0.0

@router.post("/run", response_model=SimulationResult)
def run_simulation(request: SimulationRequest, db: Session = Depends(get_db)):
    meteorology = db.query(Meteorology).filter(
        Meteorology.id == request.meteorology_id
    ).first()
    if not meteorology:
        raise HTTPException(status_code=404, detail="气象场未找到")
    
    if request.source_ids:
        sources = db.query(EmissionSource).filter(
            EmissionSource.id.in_(request.source_ids)
        ).all()
    else:
        sources = db.query(EmissionSource).filter(EmissionSource.is_active == True).all()
    
    if not sources:
        raise HTTPException(status_code=400, detail="没有可用的排放源")
    
    if request.receptor_ids:
        receptors = db.query(Receptor).filter(
            Receptor.id.in_(request.receptor_ids)
        ).all()
    else:
        receptors = db.query(Receptor).filter(Receptor.is_active == True).all()
    
    model = GaussianPlumeModel(
        wind_speed=meteorology.wind_speed,
        wind_direction=meteorology.wind_direction,
        stability_class=meteorology.stability_class,
        temperature=meteorology.temperature,
        boundary_layer_height=meteorology.boundary_layer_height,
        humidity=meteorology.humidity,
        cloud_cover=meteorology.cloud_cover,
        precipitation=meteorology.precipitation
    )
    
    all_lats = [s.latitude for s in sources] + [r.latitude for r in receptors]
    all_lons = [s.longitude for s in sources] + [r.longitude for r in receptors]
    
    if not all_lats:
        raise HTTPException(status_code=400, detail="没有有效的坐标数据")
    
    min_lat, max_lat = min(all_lats), max(all_lats)
    min_lon, max_lon = min(all_lons), max(all_lons)
    
    center_lat = (min_lat + max_lat) / 2
    center_lon = (min_lon + max_lon) / 2
    
    grid_resolution = request.grid_resolution
    domain_size = request.domain_size
    
    lat_span = max_lat - min_lat
    lon_span = max_lon - min_lon
    
    meters_per_degree = 111000
    required_lat_range = max(domain_size / meters_per_degree, lat_span * 1.5 + 0.1)
    required_lon_range = max(domain_size / (meters_per_degree * np.cos(np.radians(center_lat))), lon_span * 1.5 + 0.1)
    
    grid_points = int(max(required_lat_range, required_lon_range) * meters_per_degree / grid_resolution)
    grid_points = max(grid_points, 50)
    grid_points = min(grid_points, 500)
    
    grid_lat = np.linspace(center_lat - required_lat_range/2, center_lat + required_lat_range/2, grid_points)
    grid_lon = np.linspace(center_lon - required_lon_range/2, center_lon + required_lon_range/2, grid_points)
    
    total_concentration = np.zeros((grid_points, grid_points))
    source_contributions = []
    source_conc_fields = []
    source_pollutant_conc_fields = []
    pollutant_concentrations = {}
    all_pollutants = set()
    
    pollutant_type = request.pollutant_type
    
    for source in sources:
        source_pollutant_data = {}
        total_emission_rate = 0.0
        source_pollutants = []
        source_type = getattr(source, 'source_type', 'point')
        
        if source.pollutants:
            for p in source.pollutants:
                if pollutant_type:
                    if p.pollutant_type == pollutant_type:
                        if source_type == 'equivalent_area' and p.concentration is not None and p.concentration > 0:
                            try:
                                area_length = getattr(source, 'area_length', 100) or 100
                                area_width = getattr(source, 'area_width', 100) or 100
                                area_height = getattr(source, 'area_height', 0) or 0
                                
                                equivalent_emission_rate = model.calculate_equivalent_emission_rate(
                                    concentration=p.concentration,
                                    area_length=area_length,
                                    area_width=area_width,
                                    area_height=area_height
                                )
                                total_emission_rate += equivalent_emission_rate
                                source_pollutants.append(p.pollutant_type)
                                if p.pollutant_type not in source_pollutant_data:
                                    source_pollutant_data[p.pollutant_type] = 0
                                source_pollutant_data[p.pollutant_type] += equivalent_emission_rate
                                all_pollutants.add(p.pollutant_type)
                            except Exception as e:
                                logger.warning(f"等效面源 {source.name} 浓度转换失败: {e}")
                                continue
                        else:
                            total_emission_rate += p.emission_rate
                            source_pollutants.append(p.pollutant_type)
                            if p.pollutant_type not in source_pollutant_data:
                                source_pollutant_data[p.pollutant_type] = 0
                            source_pollutant_data[p.pollutant_type] += p.emission_rate
                            all_pollutants.add(p.pollutant_type)
                else:
                    if source_type == 'equivalent_area' and p.concentration is not None and p.concentration > 0:
                        try:
                            area_length = getattr(source, 'area_length', 100) or 100
                            area_width = getattr(source, 'area_width', 100) or 100
                            area_height = getattr(source, 'area_height', 0) or 0
                            
                            equivalent_emission_rate = model.calculate_equivalent_emission_rate(
                                concentration=p.concentration,
                                area_length=area_length,
                                area_width=area_width,
                                area_height=area_height
                            )
                            total_emission_rate += equivalent_emission_rate
                            source_pollutants.append(p.pollutant_type)
                            if p.pollutant_type not in source_pollutant_data:
                                source_pollutant_data[p.pollutant_type] = 0
                            source_pollutant_data[p.pollutant_type] += equivalent_emission_rate
                            all_pollutants.add(p.pollutant_type)
                        except Exception as e:
                            logger.warning(f"等效面源 {source.name} 浓度转换失败: {e}")
                            continue
                    else:
                        total_emission_rate += p.emission_rate
                        source_pollutants.append(p.pollutant_type)
                        if p.pollutant_type not in source_pollutant_data:
                            source_pollutant_data[p.pollutant_type] = 0
                        source_pollutant_data[p.pollutant_type] += p.emission_rate
                        all_pollutants.add(p.pollutant_type)
        
        if total_emission_rate <= 0:
            continue
        
        if source_type == 'point':
            source_conc = model.calculate_concentration_field(
                source_lat=source.latitude,
                source_lon=source.longitude,
                source_height=source.height,
                emission_rate=total_emission_rate,
                grid_lat=grid_lat,
                grid_lon=grid_lon,
                temperature=source.temperature,
                velocity=source.velocity,
                diameter=source.diameter,
                receptor_height=request.receptor_height,
                pollutant_type=pollutant_type
            )
        elif source_type == 'area':
            area_length = getattr(source, 'area_length', 100) or 100
            area_width = getattr(source, 'area_width', 100) or 100
            area_height = getattr(source, 'area_height', 0) or 0
            sigma_z0_area = getattr(source, 'sigma_z0_area', None)
            
            source_conc = model.calculate_area_source_concentration_field(
                center_lat=source.latitude,
                center_lon=source.longitude,
                area_length=area_length,
                area_width=area_width,
                area_height=area_height,
                emission_rate=total_emission_rate,
                grid_lat=grid_lat,
                grid_lon=grid_lon,
                sigma_z0=sigma_z0_area,
                receptor_height=request.receptor_height,
                pollutant_type=pollutant_type
            )
        elif source_type == 'equivalent_area':
            area_length = getattr(source, 'area_length', 100) or 100
            area_width = getattr(source, 'area_width', 100) or 100
            area_height = getattr(source, 'area_height', 0) or 0
            sigma_z0_area = getattr(source, 'sigma_z0_area', None)
            
            max_conc = None
            if source.pollutants:
                for p in source.pollutants:
                    if pollutant_type:
                        if p.pollutant_type == pollutant_type and p.concentration is not None:
                            max_conc = p.concentration
                            break
                    else:
                        if p.concentration is not None:
                            max_conc = p.concentration if max_conc is None else max(max_conc, p.concentration)
            
            if max_conc is None or max_conc <= 0 or total_emission_rate <= 0:
                source_conc = np.zeros((len(grid_lat), len(grid_lon)))
            else:
                source_conc = model.calculate_area_source_concentration_field(
                    center_lat=source.latitude,
                    center_lon=source.longitude,
                    area_length=area_length,
                    area_width=area_width,
                    area_height=area_height,
                    emission_rate=total_emission_rate,
                    grid_lat=grid_lat,
                    grid_lon=grid_lon,
                    sigma_z0=sigma_z0_area,
                    max_concentration=max_conc,
                    is_equivalent=True,
                    receptor_height=request.receptor_height,
                    pollutant_type=pollutant_type
                )
        elif source_type == 'line':
            start_lat = getattr(source, 'start_lat', source.latitude) or source.latitude
            start_lon = getattr(source, 'start_lon', source.longitude) or source.longitude
            end_lat = getattr(source, 'end_lat', source.latitude) or source.latitude
            end_lon = getattr(source, 'end_lon', source.longitude) or source.longitude
            line_width = getattr(source, 'line_width', 10) or 10
            line_height = getattr(source, 'line_height', 0) or 0
            segment_length = getattr(source, 'line_segment_length', 10) or 10
            sigma_z0_line = getattr(source, 'sigma_z0_line', None)
            
            source_conc = model.calculate_line_source_concentration_field(
                start_lat=start_lat,
                start_lon=start_lon,
                end_lat=end_lat,
                end_lon=end_lon,
                line_width=line_width,
                line_height=line_height,
                emission_rate=total_emission_rate,
                grid_lat=grid_lat,
                grid_lon=grid_lon,
                segment_length=segment_length,
                sigma_z0=sigma_z0_line,
                receptor_height=request.receptor_height,
                pollutant_type=pollutant_type
            )
        else:
            source_conc = model.calculate_concentration_field(
                source_lat=source.latitude,
                source_lon=source.longitude,
                source_height=source.height,
                emission_rate=total_emission_rate,
                grid_lat=grid_lat,
                grid_lon=grid_lon,
                temperature=source.temperature,
                velocity=source.velocity,
                diameter=source.diameter,
                receptor_height=request.receptor_height
            )
        
        total_concentration += source_conc
        source_conc_fields.append(source_conc)
        
        source_p_conc = {}
        for p_type, p_rate in source_pollutant_data.items():
            if p_rate > 0:
                if source_type == 'point':
                    p_conc = model.calculate_concentration_field(
                        source_lat=source.latitude,
                        source_lon=source.longitude,
                        source_height=source.height,
                        emission_rate=p_rate,
                        grid_lat=grid_lat,
                        grid_lon=grid_lon,
                        temperature=source.temperature,
                        velocity=source.velocity,
                        diameter=source.diameter,
                        receptor_height=request.receptor_height
                    )
                elif source_type == 'area':
                    p_conc = model.calculate_area_source_concentration_field(
                        center_lat=source.latitude,
                        center_lon=source.longitude,
                        area_length=area_length,
                        area_width=area_width,
                        area_height=area_height,
                        emission_rate=p_rate,
                        grid_lat=grid_lat,
                        grid_lon=grid_lon,
                        sigma_z0=sigma_z0_area,
                        receptor_height=request.receptor_height
                    )
                elif source_type == 'line':
                    p_conc = model.calculate_line_source_concentration_field(
                        start_lat=start_lat,
                        start_lon=start_lon,
                        end_lat=end_lat,
                        end_lon=end_lon,
                        line_width=line_width,
                        line_height=line_height,
                        emission_rate=p_rate,
                        grid_lat=grid_lat,
                        grid_lon=grid_lon,
                        segment_length=segment_length,
                        sigma_z0=sigma_z0_line,
                        receptor_height=request.receptor_height,
                        pollutant_type=p_type
                    )
                else:
                    p_conc = model.calculate_concentration_field(
                        source_lat=source.latitude,
                        source_lon=source.longitude,
                        source_height=source.height,
                        emission_rate=p_rate,
                        grid_lat=grid_lat,
                        grid_lon=grid_lon,
                        temperature=source.temperature,
                        velocity=source.velocity,
                        diameter=source.diameter,
                        receptor_height=request.receptor_height
                    )
                source_p_conc[p_type] = p_conc
                if p_type not in pollutant_concentrations:
                    pollutant_concentrations[p_type] = np.zeros((grid_points, grid_points))
                pollutant_concentrations[p_type] += p_conc
        
        source_pollutant_conc_fields.append(source_p_conc)
        
        total_mass = np.sum(source_conc)
        source_contributions.append({
            "source_id": source.id,
            "source_name": source.name,
            "total_concentration": float(total_mass),
            "max_concentration": float(np.max(source_conc)),
            "pollutants": list(set(source_pollutants)) if source_pollutants else ["Unknown"]
        })
    
    logger.info(f"开始计算受体贡献，受体数量: {len(receptors)}，污染物类型: {all_pollutants}")
    receptor_contributions = {}
    for receptor in receptors:
        logger.info(f"处理受体: {receptor.name}, 位置: ({receptor.latitude}, {receptor.longitude})")
        pollutant_receptor_data = {}
        
        for p_type in all_pollutants:
            logger.debug(f"  处理污染物类型: {p_type}")
            p_source_data = []
            p_total = 0.0
            
            for source in sources:
                source_emission_rate = 0.0
                source_type = getattr(source, 'source_type', 'point')
                
                if source.pollutants:
                    for p in source.pollutants:
                        if p.pollutant_type == p_type:
                            if source_type == 'equivalent_area' and p.concentration is not None and p.concentration > 0:
                                try:
                                    area_length = getattr(source, 'area_length', 100) or 100
                                    area_width = getattr(source, 'area_width', 100) or 100
                                    area_height = getattr(source, 'area_height', 0) or 0
                                    
                                    source_emission_rate = model.calculate_equivalent_emission_rate(
                                        concentration=p.concentration,
                                        area_length=area_length,
                                        area_width=area_width,
                                        area_height=area_height
                                    )
                                except Exception:
                                    source_emission_rate = 0
                            else:
                                source_emission_rate += p.emission_rate
                
                if source_emission_rate > 0:
                    source_type = getattr(source, 'source_type', 'point')
                    
                    if source_type == 'point':
                        conc = model.calculate_receptor_concentration(
                            source_lat=source.latitude,
                            source_lon=source.longitude,
                            source_height=source.height,
                            emission_rate=source_emission_rate,
                            receptor_lat=receptor.latitude,
                            receptor_lon=receptor.longitude,
                            receptor_height=receptor.height,
                            temperature=source.temperature,
                            velocity=source.velocity,
                            diameter=source.diameter,
                            pollutant_type=p_type
                        )
                    elif source_type == 'area':
                        area_length = getattr(source, 'area_length', 100) or 100
                        area_width = getattr(source, 'area_width', 100) or 100
                        area_height = getattr(source, 'area_height', 0) or 0
                        sigma_z0_area = getattr(source, 'sigma_z0_area', None)
                        
                        conc = model.calculate_area_source_receptor_concentration(
                            center_lat=source.latitude,
                            center_lon=source.longitude,
                            area_length=area_length,
                            area_width=area_width,
                            area_height=area_height,
                            emission_rate=source_emission_rate,
                            receptor_lat=receptor.latitude,
                            receptor_lon=receptor.longitude,
                            sigma_z0=sigma_z0_area,
                            receptor_height=receptor.height,
                            pollutant_type=p_type
                        )
                    elif source_type == 'line':
                        start_lat = getattr(source, 'start_lat', source.latitude) or source.latitude
                        start_lon = getattr(source, 'start_lon', source.longitude) or source.longitude
                        end_lat = getattr(source, 'end_lat', source.latitude) or source.latitude
                        end_lon = getattr(source, 'end_lon', source.longitude) or source.longitude
                        line_width = getattr(source, 'line_width', 10) or 10
                        line_height = getattr(source, 'line_height', 0) or 0
                        segment_length = getattr(source, 'line_segment_length', 10) or 10
                        sigma_z0_line = getattr(source, 'sigma_z0_line', None)
                        
                        conc = model.calculate_line_source_receptor_concentration(
                            start_lat=start_lat,
                            start_lon=start_lon,
                            end_lat=end_lat,
                            end_lon=end_lon,
                            line_width=line_width,
                            line_height=line_height,
                            emission_rate=source_emission_rate,
                            receptor_lat=receptor.latitude,
                            receptor_lon=receptor.longitude,
                            segment_length=segment_length,
                            sigma_z0=sigma_z0_line,
                            pollutant_type=p_type
                        )
                    elif source_type == 'equivalent_area':
                        area_length = getattr(source, 'area_length', 100) or 100
                        area_width = getattr(source, 'area_width', 100) or 100
                        area_height = getattr(source, 'area_height', 0) or 0
                        sigma_z0_area = getattr(source, 'sigma_z0_area', None)
                        
                        measured_conc = None
                        for p in source.pollutants:
                            if p.pollutant_type == p_type and p.concentration is not None:
                                measured_conc = p.concentration
                                break
                        
                        if measured_conc is None or measured_conc <= 0:
                            conc = 0.0
                        elif source_emission_rate <= 0:
                            conc = 0.0
                        else:
                            conc = model.calculate_area_source_receptor_concentration(
                                center_lat=source.latitude,
                                center_lon=source.longitude,
                                area_length=area_length,
                                area_width=area_width,
                                area_height=area_height,
                                emission_rate=source_emission_rate,
                                receptor_lat=receptor.latitude,
                                receptor_lon=receptor.longitude,
                                receptor_height=receptor.height,
                                sigma_z0=sigma_z0_area,
                                concentration=measured_conc,
                                is_equivalent=True,
                                pollutant_type=p_type
                            )
                    else:
                        conc = model.calculate_receptor_concentration(
                            source_lat=source.latitude,
                            source_lon=source.longitude,
                            source_height=source.height,
                            emission_rate=source_emission_rate,
                            receptor_lat=receptor.latitude,
                            receptor_lon=receptor.longitude,
                            receptor_height=receptor.height,
                            temperature=source.temperature,
                            velocity=source.velocity,
                            diameter=source.diameter,
                            pollutant_type=p_type
                        )
                    
                    if conc < 1e-6:
                        conc = 0.0
                    p_total += conc
                    p_source_data.append({
                        "source_id": source.id,
                        "source_name": source.name,
                        "concentration": float(conc),
                        "pollutant": p_type
                    })
            
            for item in p_source_data:
                item["percentage"] = (item["concentration"] / p_total * 100) if p_total > 0 else 0
            
            p_source_data.sort(key=lambda x: x["concentration"], reverse=True)
            pollutant_receptor_data[p_type] = p_source_data
        
        receptor_contributions[receptor.name] = pollutant_receptor_data
    
    pollutant_conc_dict = {p: c.tolist() for p, c in pollutant_concentrations.items()}
    available_pollutants = list(all_pollutants) if all_pollutants else None
    
    return SimulationResult(
        concentrations=total_concentration.tolist(),
        grid_lat=grid_lat.tolist(),
        grid_lon=grid_lon.tolist(),
        contributions=[],
        receptor_contributions=receptor_contributions,
        pollutant_concentrations=pollutant_conc_dict if pollutant_conc_dict else None,
        available_pollutants=available_pollutants
    )

@router.post("/run_with_wind", response_model=SimulationResult)
def run_simulation_with_wind(request: SimulationWithWindRequest, db: Session = Depends(get_db)):
    meteorology = db.query(Meteorology).filter(
        Meteorology.id == request.meteorology_id
    ).first()
    if not meteorology:
        raise HTTPException(status_code=404, detail="气象场未找到")
    
    if request.source_ids:
        sources = db.query(EmissionSource).filter(
            EmissionSource.id.in_(request.source_ids)
        ).all()
    else:
        sources = db.query(EmissionSource).filter(EmissionSource.is_active == True).all()
    
    if not sources:
        raise HTTPException(status_code=400, detail="没有可用的排放源")
    
    if request.receptor_ids:
        receptors = db.query(Receptor).filter(
            Receptor.id.in_(request.receptor_ids)
        ).all()
    else:
        receptors = db.query(Receptor).filter(Receptor.is_active == True).all()
    
    model = GaussianPlumeModel(
        wind_speed=request.wind_speed,
        wind_direction=request.wind_direction,
        stability_class=meteorology.stability_class,
        temperature=meteorology.temperature,
        boundary_layer_height=meteorology.boundary_layer_height,
        humidity=meteorology.humidity,
        cloud_cover=meteorology.cloud_cover,
        precipitation=meteorology.precipitation
    )
    
    center_lat = np.mean([s.latitude for s in sources]) if sources else 39.9
    center_lon = np.mean([s.longitude for s in sources]) if sources else 116.4
    
    domain_size = request.domain_size
    grid_resolution = request.grid_resolution
    grid_points = int(domain_size / grid_resolution) + 1
    
    lat_offset = domain_size / 111000 / 2
    lon_offset = domain_size / (111000 * np.cos(np.radians(center_lat))) / 2
    
    grid_lat = np.linspace(center_lat - lat_offset, center_lat + lat_offset, grid_points)
    grid_lon = np.linspace(center_lon - lon_offset, center_lon + lon_offset, grid_points)
    
    total_concentration = np.zeros((grid_points, grid_points))
    source_contributions = []
    source_conc_fields = []
    source_pollutant_conc_fields = []
    pollutant_concentrations = {}
    all_pollutants = set()
    
    pollutant_type = request.pollutant_type
    
    for source in sources:
        source_pollutant_data = {}
        total_emission_rate = 0.0
        source_pollutants = []
        source_type = getattr(source, 'source_type', 'point')
        
        if source.pollutants:
            for p in source.pollutants:
                if pollutant_type:
                    if p.pollutant_type == pollutant_type:
                        if source_type == 'equivalent_area' and p.concentration is not None and p.concentration > 0:
                            try:
                                area_length = getattr(source, 'area_length', 100) or 100
                                area_width = getattr(source, 'area_width', 100) or 100
                                area_height = getattr(source, 'area_height', 0) or 0
                                
                                equivalent_emission_rate = model.calculate_equivalent_emission_rate(
                                    concentration=p.concentration,
                                    area_length=area_length,
                                    area_width=area_width,
                                    area_height=area_height
                                )
                                total_emission_rate += equivalent_emission_rate
                                source_pollutants.append(p.pollutant_type)
                                if p.pollutant_type not in source_pollutant_data:
                                    source_pollutant_data[p.pollutant_type] = 0
                                source_pollutant_data[p.pollutant_type] += equivalent_emission_rate
                                all_pollutants.add(p.pollutant_type)
                            except Exception as e:
                                logger.warning(f"等效面源 {source.name} 浓度转换失败: {e}")
                                continue
                        else:
                            total_emission_rate += p.emission_rate
                            source_pollutants.append(p.pollutant_type)
                            if p.pollutant_type not in source_pollutant_data:
                                source_pollutant_data[p.pollutant_type] = 0
                            source_pollutant_data[p.pollutant_type] += p.emission_rate
                            all_pollutants.add(p.pollutant_type)
                else:
                    if source_type == 'equivalent_area' and p.concentration is not None and p.concentration > 0:
                        try:
                            area_length = getattr(source, 'area_length', 100) or 100
                            area_width = getattr(source, 'area_width', 100) or 100
                            area_height = getattr(source, 'area_height', 0) or 0
                            
                            equivalent_emission_rate = model.calculate_equivalent_emission_rate(
                                concentration=p.concentration,
                                area_length=area_length,
                                area_width=area_width,
                                area_height=area_height
                            )
                            total_emission_rate += equivalent_emission_rate
                            source_pollutants.append(p.pollutant_type)
                            if p.pollutant_type not in source_pollutant_data:
                                source_pollutant_data[p.pollutant_type] = 0
                            source_pollutant_data[p.pollutant_type] += equivalent_emission_rate
                            all_pollutants.add(p.pollutant_type)
                        except Exception as e:
                            logger.warning(f"等效面源 {source.name} 浓度转换失败: {e}")
                            continue
                    else:
                        total_emission_rate += p.emission_rate
                        source_pollutants.append(p.pollutant_type)
                        if p.pollutant_type not in source_pollutant_data:
                            source_pollutant_data[p.pollutant_type] = 0
                        source_pollutant_data[p.pollutant_type] += p.emission_rate
                        all_pollutants.add(p.pollutant_type)
        
        if total_emission_rate <= 0:
            continue
        
        if source_type == 'point':
            source_conc = model.calculate_concentration_field(
                source_lat=source.latitude,
                source_lon=source.longitude,
                source_height=source.height,
                emission_rate=total_emission_rate,
                grid_lat=grid_lat,
                grid_lon=grid_lon,
                temperature=source.temperature,
                velocity=source.velocity,
                diameter=source.diameter,
                receptor_height=request.receptor_height,
                pollutant_type=pollutant_type
            )
        elif source_type == 'area':
            area_length = getattr(source, 'area_length', 100) or 100
            area_width = getattr(source, 'area_width', 100) or 100
            area_height = getattr(source, 'area_height', 0) or 0
            sigma_z0_area = getattr(source, 'sigma_z0_area', None)
            
            source_conc = model.calculate_area_source_concentration_field(
                center_lat=source.latitude,
                center_lon=source.longitude,
                area_length=area_length,
                area_width=area_width,
                area_height=area_height,
                emission_rate=total_emission_rate,
                grid_lat=grid_lat,
                grid_lon=grid_lon,
                sigma_z0=sigma_z0_area,
                receptor_height=request.receptor_height
            )
        elif source_type == 'line':
            start_lat = getattr(source, 'start_lat', source.latitude) or source.latitude
            start_lon = getattr(source, 'start_lon', source.longitude) or source.longitude
            end_lat = getattr(source, 'end_lat', source.latitude) or source.latitude
            end_lon = getattr(source, 'end_lon', source.longitude) or source.longitude
            line_width = getattr(source, 'line_width', 10) or 10
            line_height = getattr(source, 'line_height', 0) or 0
            segment_length = getattr(source, 'line_segment_length', 10) or 10
            sigma_z0_line = getattr(source, 'sigma_z0_line', None)
            
            source_conc = model.calculate_line_source_concentration_field(
                start_lat=start_lat,
                start_lon=start_lon,
                end_lat=end_lat,
                end_lon=end_lon,
                line_width=line_width,
                line_height=line_height,
                emission_rate=total_emission_rate,
                grid_lat=grid_lat,
                grid_lon=grid_lon,
                segment_length=segment_length,
                sigma_z0=sigma_z0_line,
                receptor_height=request.receptor_height,
                pollutant_type=pollutant_type
            )
        elif source_type == 'equivalent_area':
            area_length = getattr(source, 'area_length', 100) or 100
            area_width = getattr(source, 'area_width', 100) or 100
            area_height = getattr(source, 'area_height', 0) or 0
            sigma_z0_area = getattr(source, 'sigma_z0_area', None)
            
            max_conc = None
            if source.pollutants:
                for p in source.pollutants:
                    if pollutant_type:
                        if p.pollutant_type == pollutant_type and p.concentration is not None:
                            max_conc = p.concentration
                            break
                    else:
                        if p.concentration is not None:
                            max_conc = p.concentration if max_conc is None else max(max_conc, p.concentration)
            
            if max_conc is None or max_conc <= 0 or total_emission_rate <= 0:
                source_conc = np.zeros((len(grid_lat), len(grid_lon)))
            else:
                source_conc = model.calculate_area_source_concentration_field(
                    center_lat=source.latitude,
                    center_lon=source.longitude,
                    area_length=area_length,
                    area_width=area_width,
                    area_height=area_height,
                    emission_rate=total_emission_rate,
                    grid_lat=grid_lat,
                    grid_lon=grid_lon,
                    sigma_z0=sigma_z0_area,
                    max_concentration=max_conc,
                    is_equivalent=True,
                    receptor_height=request.receptor_height,
                    pollutant_type=pollutant_type
                )
        else:
            source_conc = model.calculate_concentration_field(
                source_lat=source.latitude,
                source_lon=source.longitude,
                source_height=source.height,
                emission_rate=total_emission_rate,
                grid_lat=grid_lat,
                grid_lon=grid_lon,
                temperature=source.temperature,
                velocity=source.velocity,
                diameter=source.diameter,
                receptor_height=request.receptor_height,
                pollutant_type=pollutant_type
            )
        
        total_concentration += source_conc
        source_conc_fields.append(source_conc.tolist())
        source_pollutant_conc_fields.append(source_pollutant_data)
        
        for p_type, p_rate in source_pollutant_data.items():
            if p_type not in pollutant_concentrations:
                pollutant_concentrations[p_type] = np.zeros((grid_points, grid_points))
            p_fraction = p_rate / total_emission_rate if total_emission_rate > 0 else 0
            pollutant_concentrations[p_type] += source_conc * p_fraction
        
        source_contributions.append({
            "source_id": source.id,
            "source_name": source.name,
            "source_type": source_type,
            "total_emission_rate": total_emission_rate,
            "avg_concentration": float(np.mean(source_conc)),
            "max_concentration": float(np.max(source_conc)),
            "pollutants": list(set(source_pollutants)) if source_pollutants else ["Unknown"]
        })
    
    receptor_contributions = {}
    for receptor in receptors:
        pollutant_receptor_data = {}
        
        for p_type in all_pollutants:
            p_source_data = []
            p_total = 0.0
            
            for source in sources:
                source_emission_rate = 0.0
                source_type = getattr(source, 'source_type', 'point')
                
                if source.pollutants:
                    for p in source.pollutants:
                        if p.pollutant_type == p_type:
                            if source_type == 'equivalent_area' and p.concentration is not None and p.concentration > 0:
                                try:
                                    area_length = getattr(source, 'area_length', 100) or 100
                                    area_width = getattr(source, 'area_width', 100) or 100
                                    area_height = getattr(source, 'area_height', 0) or 0
                                    
                                    source_emission_rate = model.calculate_equivalent_emission_rate(
                                        concentration=p.concentration,
                                        area_length=area_length,
                                        area_width=area_width,
                                        area_height=area_height
                                    )
                                except Exception:
                                    source_emission_rate = 0
                            else:
                                source_emission_rate += p.emission_rate
                
                if source_emission_rate > 0:
                    if source_type == 'point':
                        conc = model.calculate_receptor_concentration(
                            source_lat=source.latitude,
                            source_lon=source.longitude,
                            source_height=source.height,
                            emission_rate=source_emission_rate,
                            receptor_lat=receptor.latitude,
                            receptor_lon=receptor.longitude,
                            receptor_height=receptor.height,
                            temperature=source.temperature,
                            velocity=source.velocity,
                            diameter=source.diameter,
                            pollutant_type=p_type
                        )
                    elif source_type == 'area':
                        area_length = getattr(source, 'area_length', 100) or 100
                        area_width = getattr(source, 'area_width', 100) or 100
                        area_height = getattr(source, 'area_height', 0) or 0
                        sigma_z0_area = getattr(source, 'sigma_z0_area', None)
                        
                        conc = model.calculate_area_source_receptor_concentration(
                            center_lat=source.latitude,
                            center_lon=source.longitude,
                            area_length=area_length,
                            area_width=area_width,
                            area_height=area_height,
                            emission_rate=source_emission_rate,
                            receptor_lat=receptor.latitude,
                            receptor_lon=receptor.longitude,
                            sigma_z0=sigma_z0_area,
                            receptor_height=receptor.height,
                            pollutant_type=p_type
                        )
                    elif source_type == 'line':
                        start_lat = getattr(source, 'start_lat', source.latitude) or source.latitude
                        start_lon = getattr(source, 'start_lon', source.longitude) or source.longitude
                        end_lat = getattr(source, 'end_lat', source.latitude) or source.latitude
                        end_lon = getattr(source, 'end_lon', source.longitude) or source.longitude
                        line_width = getattr(source, 'line_width', 10) or 10
                        line_height = getattr(source, 'line_height', 0) or 0
                        segment_length = getattr(source, 'line_segment_length', 10) or 10
                        sigma_z0_line = getattr(source, 'sigma_z0_line', None)
                        
                        conc = model.calculate_line_source_receptor_concentration(
                            start_lat=start_lat,
                            start_lon=start_lon,
                            end_lat=end_lat,
                            end_lon=end_lon,
                            line_width=line_width,
                            line_height=line_height,
                            emission_rate=source_emission_rate,
                            receptor_lat=receptor.latitude,
                            receptor_lon=receptor.longitude,
                            segment_length=segment_length,
                            sigma_z0=sigma_z0_line,
                            pollutant_type=p_type
                        )
                    elif source_type == 'equivalent_area':
                        area_length = getattr(source, 'area_length', 100) or 100
                        area_width = getattr(source, 'area_width', 100) or 100
                        area_height = getattr(source, 'area_height', 0) or 0
                        sigma_z0_area = getattr(source, 'sigma_z0_area', None)
                        
                        measured_conc = None
                        for p in source.pollutants:
                            if p.pollutant_type == p_type and p.concentration is not None:
                                measured_conc = p.concentration
                                break
                        
                        if measured_conc is None or measured_conc <= 0:
                            conc = 0.0
                        elif source_emission_rate <= 0:
                            conc = 0.0
                        else:
                            conc = model.calculate_area_source_receptor_concentration(
                                center_lat=source.latitude,
                                center_lon=source.longitude,
                                area_length=area_length,
                                area_width=area_width,
                                area_height=area_height,
                                emission_rate=source_emission_rate,
                                receptor_lat=receptor.latitude,
                                receptor_lon=receptor.longitude,
                                receptor_height=receptor.height,
                                sigma_z0=sigma_z0_area,
                                concentration=measured_conc,
                                is_equivalent=True,
                                pollutant_type=p_type
                            )
                    else:
                        conc = model.calculate_receptor_concentration(
                            source_lat=source.latitude,
                            source_lon=source.longitude,
                            source_height=source.height,
                            emission_rate=source_emission_rate,
                            receptor_lat=receptor.latitude,
                            receptor_lon=receptor.longitude,
                            receptor_height=receptor.height,
                            temperature=source.temperature,
                            velocity=source.velocity,
                            diameter=source.diameter,
                            pollutant_type=p_type
                        )
                    
                    if conc < 1e-6:
                        conc = 0.0
                    p_total += conc
                    p_source_data.append({
                        "source_id": source.id,
                        "source_name": source.name,
                        "concentration": float(conc),
                        "pollutant": p_type
                    })
            
            for item in p_source_data:
                item["percentage"] = (item["concentration"] / p_total * 100) if p_total > 0 else 0
            
            p_source_data.sort(key=lambda x: x["concentration"], reverse=True)
            pollutant_receptor_data[p_type] = p_source_data
        
        receptor_contributions[receptor.name] = pollutant_receptor_data
    
    pollutant_conc_dict = {p: c.tolist() for p, c in pollutant_concentrations.items()}
    available_pollutants = list(all_pollutants) if all_pollutants else None
    
    return SimulationResult(
        concentrations=total_concentration.tolist(),
        grid_lat=grid_lat.tolist(),
        grid_lon=grid_lon.tolist(),
        contributions=[],
        receptor_contributions=receptor_contributions,
        pollutant_concentrations=pollutant_conc_dict if pollutant_conc_dict else None,
        available_pollutants=available_pollutants
    )

@router.get("/preview/{meteorology_id}/{source_id}")
def preview_plume(
    meteorology_id: int, 
    source_id: int, 
    domain_size: float = 5000.0,
    grid_resolution: float = 50.0,
    db: Session = Depends(get_db)
):
    meteorology = db.query(Meteorology).filter(
        Meteorology.id == meteorology_id
    ).first()
    if not meteorology:
        raise HTTPException(status_code=404, detail="气象场未找到")
    
    source = db.query(EmissionSource).filter(EmissionSource.id == source_id).first()
    if not source:
        raise HTTPException(status_code=404, detail="排放源未找到")
    
    total_emission_rate = 0.0
    if source.pollutants:
        for p in source.pollutants:
            total_emission_rate += p.emission_rate
    
    if total_emission_rate <= 0:
        total_emission_rate = 1.0
    
    model = GaussianPlumeModel(
        wind_speed=meteorology.wind_speed,
        wind_direction=meteorology.wind_direction,
        stability_class=meteorology.stability_class,
        temperature=meteorology.temperature,
        boundary_layer_height=meteorology.boundary_layer_height
    )
    
    meters_per_degree = 111000
    grid_points = int(domain_size / grid_resolution)
    
    lat_range = domain_size / meters_per_degree
    lon_range = domain_size / (meters_per_degree * np.cos(np.radians(source.latitude)))
    
    grid_lat = np.linspace(
        source.latitude - lat_range/2, 
        source.latitude + lat_range/2, 
        grid_points
    )
    grid_lon = np.linspace(
        source.longitude - lon_range/2, 
        source.longitude + lon_range/2, 
        grid_points
    )
    
    concentration = model.calculate_concentration_field(
        source_lat=source.latitude,
        source_lon=source.longitude,
        source_height=source.height,
        emission_rate=total_emission_rate,
        grid_lat=grid_lat,
        grid_lon=grid_lon,
        temperature=source.temperature,
        velocity=source.velocity,
        diameter=source.diameter,
        pollutant_type=pollutant_type
    )
    
    return {
        "concentrations": concentration.tolist(),
        "grid_lat": grid_lat.tolist(),
        "grid_lon": grid_lon.tolist(),
        "source": {
            "id": source.id,
            "name": source.name,
            "latitude": source.latitude,
            "longitude": source.longitude
        },
        "meteorology": {
            "wind_speed": meteorology.wind_speed,
            "wind_direction": meteorology.wind_direction,
            "stability_class": meteorology.stability_class
        }
    }

class ParallelWindRequest(BaseModel):
    meteorology_id: int
    source_ids: Optional[List[int]] = None
    receptor_ids: Optional[List[int]] = None
    pollutant_type: Optional[str] = None
    grid_resolution: float = 10.0
    domain_size: float = 10000.0
    wind_speed: float
    wind_directions: List[float]
    weights: Optional[List[float]] = None
    receptor_height: float = 0.0
    num_workers: Optional[int] = None
    return_aggregated_only: bool = True

def process_single_wind_direction(args):
    """处理单个风向的模拟计算（工作进程函数）"""
    import warnings
    import os
    os.environ['OPENBLAS_VERBOSE'] = '0'
    warnings.filterwarnings('ignore', message='.*loaded more than 1 DLL from.*')
    warnings.filterwarnings('ignore', category=UserWarning, module='numpy')

    (wind_direction, meteorology_data, sources_data, receptors_data,
     grid_resolution, domain_size, pollutant_type, receptor_height) = args

    try:
        from backend.core.gaussian_plume import GaussianPlumeModel
        import numpy as np

        model = GaussianPlumeModel(
            wind_speed=meteorology_data['wind_speed'],
            wind_direction=wind_direction,
            stability_class=meteorology_data['stability_class'],
            temperature=meteorology_data['temperature'],
            boundary_layer_height=meteorology_data['boundary_layer_height'],
            humidity=meteorology_data['humidity'],
            cloud_cover=meteorology_data['cloud_cover'],
            precipitation=meteorology_data['precipitation']
        )

        center_lat = np.mean([s['latitude'] for s in sources_data]) if sources_data else 39.9
        center_lon = np.mean([s['longitude'] for s in sources_data]) if sources_data else 116.4

        grid_points = int(domain_size / grid_resolution) + 1

        lat_offset = domain_size / 111000 / 2
        lon_offset = domain_size / (111000 * np.cos(np.radians(center_lat))) / 2

        grid_lat = np.linspace(center_lat - lat_offset, center_lat + lat_offset, grid_points)
        grid_lon = np.linspace(center_lon - lon_offset, center_lon + lon_offset, grid_points)

        total_concentration = np.zeros((grid_points, grid_points))
        source_contributions = []
        pollutant_concentrations = {}
        all_pollutants = set()

        for source in sources_data:
            source_pollutant_data = {}
            total_emission_rate = 0.0
            source_pollutants = []
            source_type = source.get('source_type', 'point')

            for p in source.get('pollutants', []):
                if pollutant_type:
                    if p['pollutant_type'] == pollutant_type:
                        if source_type == 'equivalent_area' and p.get('concentration') is not None and p['concentration'] > 0:
                            try:
                                area_length = source.get('area_length', 100) or 100
                                area_width = source.get('area_width', 100) or 100
                                area_height = source.get('area_height', 0) or 0

                                equivalent_emission_rate = model.calculate_equivalent_emission_rate(
                                    concentration=p['concentration'],
                                    area_length=area_length,
                                    area_width=area_width,
                                    area_height=area_height
                                )
                                total_emission_rate += equivalent_emission_rate
                                source_pollutants.append(p['pollutant_type'])
                                if p['pollutant_type'] not in source_pollutant_data:
                                    source_pollutant_data[p['pollutant_type']] = 0
                                source_pollutant_data[p['pollutant_type']] += equivalent_emission_rate
                                all_pollutants.add(p['pollutant_type'])
                            except Exception:
                                continue
                        else:
                            total_emission_rate += p['emission_rate']
                            source_pollutants.append(p['pollutant_type'])
                            if p['pollutant_type'] not in source_pollutant_data:
                                source_pollutant_data[p['pollutant_type']] = 0
                            source_pollutant_data[p['pollutant_type']] += p['emission_rate']
                            all_pollutants.add(p['pollutant_type'])
                else:
                    if source_type == 'equivalent_area' and p.get('concentration') is not None and p['concentration'] > 0:
                        try:
                            area_length = source.get('area_length', 100) or 100
                            area_width = source.get('area_width', 100) or 100
                            area_height = source.get('area_height', 0) or 0

                            equivalent_emission_rate = model.calculate_equivalent_emission_rate(
                                concentration=p['concentration'],
                                area_length=area_length,
                                area_width=area_width,
                                area_height=area_height
                            )
                            total_emission_rate += equivalent_emission_rate
                            source_pollutants.append(p['pollutant_type'])
                            if p['pollutant_type'] not in source_pollutant_data:
                                source_pollutant_data[p['pollutant_type']] = 0
                            source_pollutant_data[p['pollutant_type']] += equivalent_emission_rate
                            all_pollutants.add(p['pollutant_type'])
                        except Exception:
                            continue
                    else:
                        total_emission_rate += p['emission_rate']
                        source_pollutants.append(p['pollutant_type'])
                        if p['pollutant_type'] not in source_pollutant_data:
                            source_pollutant_data[p['pollutant_type']] = 0
                        source_pollutant_data[p['pollutant_type']] += p['emission_rate']
                        all_pollutants.add(p['pollutant_type'])

            if total_emission_rate <= 0:
                continue

            if source_type == 'point':
                source_conc = model.calculate_concentration_field(
                    source_lat=source['latitude'],
                    source_lon=source['longitude'],
                    source_height=source['height'],
                    emission_rate=total_emission_rate,
                    grid_lat=grid_lat,
                    grid_lon=grid_lon,
                    temperature=source.get('temperature', 400.0),
                    velocity=source.get('velocity', 10.0),
                    diameter=source.get('diameter', 1.0),
                    receptor_height=receptor_height,
                    pollutant_type=pollutant_type
                )
            elif source_type == 'area':
                area_length = source.get('area_length', 100) or 100
                area_width = source.get('area_width', 100) or 100
                area_height = source.get('area_height', 0) or 0

                source_conc = model.calculate_area_source_concentration_field(
                    center_lat=source['latitude'],
                    center_lon=source['longitude'],
                    area_length=area_length,
                    area_width=area_width,
                    area_height=area_height,
                    emission_rate=total_emission_rate,
                    grid_lat=grid_lat,
                    grid_lon=grid_lon,
                    receptor_height=receptor_height,
                    pollutant_type=pollutant_type
                )
            elif source_type == 'line':
                start_lat = source.get('start_lat', source['latitude']) or source['latitude']
                start_lon = source.get('start_lon', source['longitude']) or source['longitude']
                end_lat = source.get('end_lat', source['latitude']) or source['latitude']
                end_lon = source.get('end_lon', source['longitude']) or source['longitude']
                line_width = source.get('line_width', 10) or 10
                line_height = source.get('line_height', 0) or 0
                segment_length = source.get('line_segment_length', 10) or 10

                source_conc = model.calculate_line_source_concentration_field(
                    start_lat=start_lat,
                    start_lon=start_lon,
                    end_lat=end_lat,
                    end_lon=end_lon,
                    line_width=line_width,
                    line_height=line_height,
                    emission_rate=total_emission_rate,
                    grid_lat=grid_lat,
                    grid_lon=grid_lon,
                    segment_length=segment_length,
                    receptor_height=receptor_height,
                    pollutant_type=pollutant_type
                )
            elif source_type == 'equivalent_area':
                area_length = source.get('area_length', 100) or 100
                area_width = source.get('area_width', 100) or 100
                area_height = source.get('area_height', 0) or 0

                max_conc = None
                for p in source.get('pollutants', []):
                    if pollutant_type:
                        if p['pollutant_type'] == pollutant_type and p.get('concentration') is not None:
                            max_conc = p['concentration']
                            break
                    else:
                        if p.get('concentration') is not None:
                            max_conc = p['concentration'] if max_conc is None else max(max_conc, p['concentration'])

                if max_conc is None or max_conc <= 0 or total_emission_rate <= 0:
                    source_conc = np.zeros((len(grid_lat), len(grid_lon)))
                else:
                    source_conc = model.calculate_area_source_concentration_field(
                        center_lat=source['latitude'],
                        center_lon=source['longitude'],
                        area_length=area_length,
                        area_width=area_width,
                        area_height=area_height,
                        emission_rate=total_emission_rate,
                        grid_lat=grid_lat,
                        grid_lon=grid_lon,
                        max_concentration=max_conc,
                        is_equivalent=True,
                        receptor_height=receptor_height,
                        pollutant_type=pollutant_type
                    )
            else:
                source_conc = model.calculate_concentration_field(
                    source_lat=source['latitude'],
                    source_lon=source['longitude'],
                    source_height=source['height'],
                    emission_rate=total_emission_rate,
                    grid_lat=grid_lat,
                    grid_lon=grid_lon,
                    temperature=source.get('temperature', 400.0),
                    velocity=source.get('velocity', 10.0),
                    diameter=source.get('diameter', 1.0),
                    receptor_height=receptor_height,
                    pollutant_type=pollutant_type
                )

            total_concentration += source_conc

            for p_type, p_rate in source_pollutant_data.items():
                if p_type not in pollutant_concentrations:
                    pollutant_concentrations[p_type] = np.zeros((grid_points, grid_points))
                p_fraction = p_rate / total_emission_rate if total_emission_rate > 0 else 0
                pollutant_concentrations[p_type] += source_conc * p_fraction

            source_contributions.append({
                "source_id": source['id'],
                "source_name": source['name'],
                "source_type": source_type,
                "total_emission_rate": total_emission_rate,
                "avg_concentration": float(np.mean(source_conc)),
                "max_concentration": float(np.max(source_conc)),
                "pollutants": list(set(source_pollutants)) if source_pollutants else ["Unknown"]
            })

        pollutant_conc_dict = {p: c.tolist() for p, c in pollutant_concentrations.items()}
        available_pollutants = list(all_pollutants) if all_pollutants else None

        receptor_contributions = {}
        
        if receptors_data:
            try:
                all_pollutants_list = list(all_pollutants) if all_pollutants else ['PM2.5']
                
                for receptor in receptors_data:
                    pollutant_receptor_data = {}
                    
                    for p_type in all_pollutants_list:
                        p_source_data = []
                        p_total = 0.0
                        
                        for source in sources_data:
                            source_emission_rate = 0.0
                            source_type = source.get('source_type', 'point')
                            
                            for p in source.get('pollutants', []):
                                if p['pollutant_type'] == p_type:
                                    if source_type == 'equivalent_area' and p.get('concentration') is not None and p['concentration'] > 0:
                                        try:
                                            area_length = source.get('area_length', 100) or 100
                                            area_width = source.get('area_width', 100) or 100
                                            area_height = source.get('area_height', 0) or 0
                                            
                                            source_emission_rate = model.calculate_equivalent_emission_rate(
                                                concentration=p['concentration'],
                                                area_length=area_length,
                                                area_width=area_width,
                                                area_height=area_height
                                            )
                                        except Exception:
                                            source_emission_rate = 0
                                    else:
                                        source_emission_rate += p.get('emission_rate', 0)
                            
                            if source_emission_rate > 0:
                                try:
                                    if source_type == 'point':
                                        conc = model.calculate_receptor_concentration(
                                            source_lat=source['latitude'],
                                            source_lon=source['longitude'],
                                            source_height=source.get('height', 50),
                                            emission_rate=source_emission_rate,
                                            receptor_lat=receptor['latitude'],
                                            receptor_lon=receptor['longitude'],
                                            receptor_height=receptor['height'],
                                            temperature=source.get('temperature', 400),
                                            velocity=source.get('velocity', 10),
                                            diameter=source.get('diameter', 1),
                                            pollutant_type=p_type
                                        )
                                    elif source_type == 'area':
                                        conc = model.calculate_area_source_receptor_concentration(
                                            center_lat=source['latitude'],
                                            center_lon=source['longitude'],
                                            area_length=source.get('area_length', 100) or 100,
                                            area_width=source.get('area_width', 100) or 100,
                                            area_height=source.get('area_height', 0) or 0,
                                            emission_rate=source_emission_rate,
                                            receptor_lat=receptor['latitude'],
                                            receptor_lon=receptor['longitude'],
                                            receptor_height=receptor['height'],
                                            pollutant_type=p_type
                                        )
                                    elif source_type == 'line':
                                        conc = model.calculate_line_source_receptor_concentration(
                                            start_lat=source.get('start_lat', source['latitude']) or source['latitude'],
                                            start_lon=source.get('start_lon', source['longitude']) or source['longitude'],
                                            end_lat=source.get('end_lat', source['latitude']) or source['latitude'],
                                            end_lon=source.get('end_lon', source['longitude']) or source['longitude'],
                                            line_width=source.get('line_width', 10) or 10,
                                            line_height=source.get('line_height', 0) or 0,
                                            emission_rate=source_emission_rate,
                                            receptor_lat=receptor['latitude'],
                                            receptor_lon=receptor['longitude'],
                                            receptor_height=receptor['height'],
                                            pollutant_type=p_type
                                        )
                                    elif source_type == 'equivalent_area':
                                        conc = model.calculate_area_source_receptor_concentration(
                                            center_lat=source['latitude'],
                                            center_lon=source['longitude'],
                                            area_length=source.get('area_length', 100) or 100,
                                            area_width=source.get('area_width', 100) or 100,
                                            area_height=source.get('area_height', 0) or 0,
                                            emission_rate=source_emission_rate,
                                            receptor_lat=receptor['latitude'],
                                            receptor_lon=receptor['longitude'],
                                            receptor_height=receptor['height'],
                                            pollutant_type=p_type
                                        )
                                    else:
                                        conc = model.calculate_receptor_concentration(
                                            source_lat=source['latitude'],
                                            source_lon=source['longitude'],
                                            source_height=source.get('height', 50),
                                            emission_rate=source_emission_rate,
                                            receptor_lat=receptor['latitude'],
                                            receptor_lon=receptor['longitude'],
                                            receptor_height=receptor['height'],
                                            temperature=source.get('temperature', 400),
                                            velocity=source.get('velocity', 10),
                                            diameter=source.get('diameter', 1),
                                            pollutant_type=p_type
                                        )
                                    
                                    if conc > 0:
                                        p_total += conc
                                        p_source_data.append({
                                            "source_id": source['id'],
                                            "source_name": source['name'],
                                            "concentration": float(conc)
                                        })
                                except Exception:
                                    continue
                        
                        if p_source_data:
                            p_source_data.sort(key=lambda x: x['concentration'], reverse=True)
                            for item in p_source_data:
                                item['percentage'] = (item['concentration'] / p_total * 100) if p_total > 0 else 0
                            
                            pollutant_receptor_data[p_type] = p_source_data
                    
                    if pollutant_receptor_data:
                        receptor_contributions[receptor['name']] = pollutant_receptor_data
            except Exception as e:
                logger.warning(f"计算受体点贡献度失败: {e}")
                receptor_contributions = {}

        return {
            "wind_direction": wind_direction,
            "success": True,
            "concentrations": total_concentration.tolist(),
            "grid_lat": grid_lat.tolist(),
            "grid_lon": grid_lon.tolist(),
            "contributions": source_contributions,
            "pollutant_concentrations": pollutant_conc_dict if pollutant_conc_dict else None,
            "available_pollutants": available_pollutants,
            "receptor_contributions": receptor_contributions
        }

    except Exception as e:
        return {
            "wind_direction": wind_direction,
            "success": False,
            "error": str(e)
        }

@router.post("/run_parallel")
def run_parallel_simulation(request: ParallelWindRequest, db: Session = Depends(get_db)):
    """
    并行计算多个风向的模拟结果

    使用多进程并行计算，充分利用多核CPU
    对于72个风向、10km范围、10m分辨率的场景：
    - 原始：72分钟（串行）
    - 优化后：2-5分钟（32核并行+向量化）
    """
    import time
    start_time = time.time()

    logger.info(f"开始并行模拟，风向数量: {len(request.wind_directions)}")

    meteorology = db.query(Meteorology).filter(
        Meteorology.id == request.meteorology_id
    ).first()
    if not meteorology:
        raise HTTPException(status_code=404, detail="气象场未找到")

    if request.source_ids:
        sources = db.query(EmissionSource).filter(
            EmissionSource.id.in_(request.source_ids)
        ).all()
    else:
        sources = db.query(EmissionSource).filter(EmissionSource.is_active == True).all()

    if not sources:
        raise HTTPException(status_code=400, detail="没有可用的排放源")

    if request.receptor_ids:
        receptors = db.query(Receptor).filter(
            Receptor.id.in_(request.receptor_ids)
        ).all()
    else:
        receptors = db.query(Receptor).filter(Receptor.is_active == True).all()

    meteorology_data = {
        'wind_speed': request.wind_speed,
        'stability_class': meteorology.stability_class,
        'temperature': meteorology.temperature,
        'boundary_layer_height': meteorology.boundary_layer_height,
        'humidity': meteorology.humidity,
        'cloud_cover': meteorology.cloud_cover,
        'precipitation': meteorology.precipitation
    }

    sources_data = []
    for source in sources:
        source_dict = {
            'id': source.id,
            'name': source.name,
            'latitude': source.latitude,
            'longitude': source.longitude,
            'height': source.height,
            'source_type': getattr(source, 'source_type', 'point'),
            'temperature': getattr(source, 'temperature', 400.0),
            'velocity': getattr(source, 'velocity', 10.0),
            'diameter': getattr(source, 'diameter', 1.0),
            'area_length': getattr(source, 'area_length', 100),
            'area_width': getattr(source, 'area_width', 100),
            'area_height': getattr(source, 'area_height', 0),
            'start_lat': getattr(source, 'start_lat', None),
            'start_lon': getattr(source, 'start_lon', None),
            'end_lat': getattr(source, 'end_lat', None),
            'end_lon': getattr(source, 'end_lon', None),
            'line_width': getattr(source, 'line_width', 10),
            'line_height': getattr(source, 'line_height', 0),
            'line_segment_length': getattr(source, 'line_segment_length', 10),
            'pollutants': []
        }
        if source.pollutants:
            for p in source.pollutants:
                source_dict['pollutants'].append({
                    'pollutant_type': p.pollutant_type,
                    'emission_rate': p.emission_rate,
                    'concentration': p.concentration
                })
        sources_data.append(source_dict)

    receptors = db.query(Receptor).filter(Receptor.is_active == True).all()
    receptors_data = []
    for receptor in receptors:
        receptors_data.append({
            'id': receptor.id,
            'name': receptor.name,
            'latitude': receptor.latitude,
            'longitude': receptor.longitude,
            'height': receptor.height
        })

    num_workers = request.num_workers if request.num_workers else min(mp.cpu_count(), len(request.wind_directions))
    logger.info(f"使用 {num_workers} 个工作进程进行并行计算")

    grid_points = int(request.domain_size / request.grid_resolution) + 1
    estimated_memory_per_result = grid_points * grid_points * 8 * 3 / (1024**2)
    total_estimated_memory = estimated_memory_per_result * len(request.wind_directions) / 1024

    logger.info(f"网格大小: {grid_points}x{grid_points}, 每个风向结果约 {estimated_memory_per_result:.1f}MB")
    logger.info(f"总数据量估算: ~{total_estimated_memory:.2f}GB ({len(request.wind_directions)}个风向)")

    if total_estimated_memory > 0.5 and not request.return_aggregated_only:
        logger.warning(f"⚠️ 数据量过大({total_estimated_memory:.2f}GB)，自动启用聚合模式")
        request.return_aggregated_only = True

    args_list = [
        (wind_dir, meteorology_data, sources_data, receptors_data,
         request.grid_resolution, request.domain_size,
         request.pollutant_type, request.receptor_height)
        for wind_dir in request.wind_directions
    ]

    results = []
    errors = []

    with ProcessPoolExecutor(max_workers=num_workers) as executor:
        futures = {executor.submit(process_single_wind_direction, args): args[0] for args in args_list}

        for future in as_completed(futures):
            wind_dir = futures[future]
            try:
                result = future.result()
                if result['success']:
                    results.append(result)
                    logger.info(f"风向 {wind_dir}° 计算完成")
                else:
                    errors.append({"wind_direction": wind_dir, "error": result.get('error', '未知错误')})
                    logger.error(f"风向 {wind_dir}° 计算失败: {result.get('error')}")
            except Exception as e:
                errors.append({"wind_direction": wind_dir, "error": str(e)})
                logger.error(f"风向 {wind_dir}° 计算异常: {e}")

    results.sort(key=lambda x: x['wind_direction'])

    elapsed_time = time.time() - start_time
    logger.info(f"并行模拟完成，总耗时: {elapsed_time:.2f}秒，成功: {len(results)}/{len(request.wind_directions)}")

    if request.return_aggregated_only:
        logger.info("执行后端聚合计算...")
        weights = request.weights if request.weights and len(request.weights) == len(results) else [1.0/len(results)]*len(results)
        total_weight = sum(weights)

        aggregated_concentrations = None
        aggregated_pollutant_concentrations = {}
        grid_lat = None
        grid_lon = None
        available_pollutants = set()

        for i, result in enumerate(results):
            weight = weights[i] / total_weight if total_weight > 0 else 1.0/len(results)
            conc = np.array(result['concentrations'])

            if aggregated_concentrations is None:
                aggregated_concentrations = conc * weight
                grid_lat = result['grid_lat']
                grid_lon = result['grid_lon']
            else:
                aggregated_concentrations += conc * weight

            if result.get('pollutant_concentrations'):
                for p_type, p_conc in result['pollutant_concentrations'].items():
                    available_pollutants.add(p_type)
                    p_conc_arr = np.array(p_conc)
                    if p_type not in aggregated_pollutant_concentrations:
                        aggregated_pollutant_concentrations[p_type] = p_conc_arr * weight
                    else:
                        aggregated_pollutant_concentrations[p_type] += p_conc_arr * weight

        logger.info("聚合受体点贡献度...")
        receptor_contributions = {}
        
        for i, result in enumerate(results):
            weight = weights[i] / total_weight if total_weight > 0 else 1.0/len(results)
            wind_receptor_contrib = result.get('receptor_contributions', {})
            
            for receptor_name, pollutant_data in wind_receptor_contrib.items():
                if receptor_name not in receptor_contributions:
                    receptor_contributions[receptor_name] = {}
                
                for p_type, sources in pollutant_data.items():
                    if p_type not in receptor_contributions[receptor_name]:
                        receptor_contributions[receptor_name][p_type] = []
                    
                    for source_info in sources:
                        weighted_conc = source_info.get('concentration', 0) * weight
                        
                        existing_source = next(
                            (s for s in receptor_contributions[receptor_name][p_type] 
                             if s.get('source_id') == source_info.get('source_id')), 
                            None
                        )
                        
                        if existing_source:
                            existing_source['concentration'] += weighted_conc
                        else:
                            receptor_contributions[receptor_name][p_type].append({
                                'source_id': source_info.get('source_id'),
                                'source_name': source_info.get('source_name'),
                                'concentration': weighted_conc
                            })
        
        for receptor_name in receptor_contributions:
            for p_type in receptor_contributions[receptor_name]:
                sources = receptor_contributions[receptor_name][p_type]
                total_conc = sum(s['concentration'] for s in sources)
                sources.sort(key=lambda x: x['concentration'], reverse=True)
                for s in sources:
                    s['percentage'] = (s['concentration'] / total_conc * 100) if total_conc > 0 else 0
        
        logger.info(f"受体点贡献度聚合完成: {len(receptor_contributions)} 个受体点")

        response_data = {
            "success": True,
            "mode": "aggregated",
            "total_wind_directions": len(request.wind_directions),
            "successful_simulations": len(results),
            "failed_simulations": len(errors),
            "errors": errors if errors else None,
            "num_workers_used": num_workers,
            "computation_time_seconds": round(elapsed_time, 2),
            "speedup_factor": round(len(request.wind_directions) * 60 / elapsed_time, 1) if elapsed_time > 0 else 0,
            "concentrations": aggregated_concentrations.tolist() if aggregated_concentrations is not None else [],
            "grid_lat": grid_lat,
            "grid_lon": grid_lon,
            "pollutant_concentrations": {k: v.tolist() for k, v in aggregated_pollutant_concentrations.items()},
            "available_pollutants": list(available_pollutants) if available_pollutants else None,
            "contributions": [],
            "receptor_contributions": receptor_contributions
        }

        import gc
        del aggregated_concentrations
        del aggregated_pollutant_concentrations
        gc.collect()

        return response_data
    else:

        return {
            "success": True,
            "mode": "detailed",
            "total_wind_directions": len(request.wind_directions),
            "successful_simulations": len(results),
            "failed_simulations": len(errors),
            "errors": errors if errors else None,
            "num_workers_used": num_workers,
            "computation_time_seconds": round(elapsed_time, 2),
            "speedup_factor": round(len(request.wind_directions) * 60 / elapsed_time, 1) if elapsed_time > 0 else 0,
            "results": results
        }
