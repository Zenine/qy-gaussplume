<template>
  <div class="table-page sources-page">
    <div class="toolbar page-toolbar">
      <el-tag type="primary" effect="plain">{{ regionName }}</el-tag>
      <el-button type="primary" icon="el-icon-plus" @click="openCreate">新增排放源</el-button>
      <el-divider direction="vertical" />
      <span>类型：</span>
      <el-select v-model="importType" style="width: 120px">
        <el-option v-for="t in sourceTypes" :key="t.value" :value="t.value" :label="t.label" />
      </el-select>
      <el-button icon="el-icon-download" @click="downloadTemplate">下载模板</el-button>
      <el-upload :auto-upload="true" :show-file-list="false" accept=".xlsx,.xls" :before-upload="importFile">
        <el-button icon="el-icon-upload2">批量导入</el-button>
      </el-upload>
      <el-button type="success" icon="el-icon-check" @click="enableAll">全部启用</el-button>
      <el-button type="warning" icon="el-icon-close" @click="disableAll">全部停用</el-button>
      <el-button type="danger" icon="el-icon-delete" :disabled="selected.length === 0" @click="removeSelected">
        批量删除 ({{ selected.length }})
      </el-button>
      <span class="spacer" />
      <span>过滤：</span>
      <el-select v-model="draftFilterType" data-test="source-type-filter" style="width: 140px">
        <el-option value="" label="全部类型" />
        <el-option v-for="t in sourceTypes" :key="t.value" :value="t.value" :label="t.label" />
      </el-select>
      <el-button data-test="apply-source-type-filter" @click="applyTypeFilter">确定</el-button>
      <el-button type="text" icon="el-icon-refresh" @click="refresh">刷新</el-button>
    </div>

    <div class="table-shell">
      <el-table
        ref="tableRef"
        v-loading="loading"
        :data="filteredItems"
        border
        stripe
        row-key="id"
        @selection-change="selected = $event"
      >
        <el-table-column type="selection" width="46" />
        <el-table-column prop="id" label="ID" width="60" />
        <el-table-column prop="name" label="名称" min-width="140" />
        <el-table-column label="类型" width="100">
          <template slot-scope="scope"><el-tag size="small">{{ labelOf(scope.row.sourceType) }}</el-tag></template>
        </el-table-column>
        <el-table-column prop="latitude" label="纬度" width="132" show-overflow-tooltip />
        <el-table-column prop="longitude" label="经度" width="132" show-overflow-tooltip />
        <el-table-column label="高度 (m)" width="100"><template slot-scope="scope">{{ scope.row.height }}</template></el-table-column>
        <el-table-column label="污染物" min-width="180">
          <template slot-scope="scope">
            <el-tag v-for="p in scope.row.pollutants" :key="p.id" size="mini" style="margin-right: 4px">
              {{ p.pollutantType }}: {{ pollutantValue(scope.row, p) }}
            </el-tag>
            <span v-if="scope.row.pollutants.length === 0" class="muted">—</span>
          </template>
        </el-table-column>
        <el-table-column label="启用" width="96">
          <template slot-scope="scope">
            <el-switch
              v-model="scope.row.isActive"
              :data-test="`source-active-${scope.row.id}`"
              @change="setActive(scope.row, $event)"
            />
          </template>
        </el-table-column>
        <el-table-column label="操作" width="150" fixed="right">
          <template slot-scope="scope">
            <el-button type="text" size="small" icon="el-icon-edit" @click="openEdit(scope.row)">编辑</el-button>
            <el-button type="text" size="small" icon="el-icon-delete" style="color: #f56c6c" @click="remove(scope.row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-dialog :visible.sync="dialogVisible" :title="dialogMode === 'create' ? '新增排放源' : '编辑排放源'" width="760px">
      <el-form :model="form" label-width="116px">
        <el-form-item label="名称" required><el-input v-model="form.name" placeholder="请输入名称" /></el-form-item>
        <el-form-item label="类型">
          <el-radio-group v-model="form.sourceType">
            <el-radio-button v-for="t in sourceTypes" :key="t.value" :label="t.value">{{ t.label }}</el-radio-button>
          </el-radio-group>
        </el-form-item>

        <el-row :gutter="12">
          <el-col :span="12"><el-form-item label="纬度" required><el-input-number v-model="form.latitude" :precision="6" :step="0.001" /></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="经度" required><el-input-number v-model="form.longitude" :precision="6" :step="0.001" /></el-form-item></el-col>
        </el-row>

        <template v-if="form.sourceType === 'point'">
          <el-row :gutter="12">
            <el-col :span="8"><el-form-item label="高度 (m)"><el-input-number v-model="form.height" :min="0" :step="1" /></el-form-item></el-col>
            <el-col :span="8"><el-form-item label="烟气温度 (K)"><el-input-number v-model="form.temperature" :min="200" :step="10" /></el-form-item></el-col>
            <el-col :span="8"><el-form-item label="出口速度 (m/s)"><el-input-number v-model="form.velocity" :min="0" :step="1" /></el-form-item></el-col>
          </el-row>
          <el-form-item label="烟囱直径 (m)"><el-input-number v-model="form.diameter" :min="0.1" :step="0.1" /></el-form-item>
        </template>

        <template v-if="form.sourceType === 'area' || form.sourceType === 'equivalent_area'">
          <el-row :gutter="12">
            <el-col :span="8"><el-form-item label="长度 (m)"><el-input-number v-model="form.areaLength" :min="1" :step="10" /></el-form-item></el-col>
            <el-col :span="8"><el-form-item label="宽度 (m)"><el-input-number v-model="form.areaWidth" :min="1" :step="10" /></el-form-item></el-col>
            <el-col :span="8"><el-form-item label="高度 (m)"><el-input-number v-model="form.areaHeight" :min="0" :step="1" /></el-form-item></el-col>
          </el-row>
          <el-form-item label="面源温度 (K)"><el-input-number v-model="form.areaTemperature" :min="200" :step="10" /></el-form-item>
        </template>

        <template v-if="form.sourceType === 'line'">
          <el-row :gutter="12">
            <el-col :span="12"><el-form-item label="起点纬度"><el-input-number v-model="form.startLat" :precision="6" :step="0.001" /></el-form-item></el-col>
            <el-col :span="12"><el-form-item label="起点经度"><el-input-number v-model="form.startLon" :precision="6" :step="0.001" /></el-form-item></el-col>
            <el-col :span="12"><el-form-item label="终点纬度"><el-input-number v-model="form.endLat" :precision="6" :step="0.001" /></el-form-item></el-col>
            <el-col :span="12"><el-form-item label="终点经度"><el-input-number v-model="form.endLon" :precision="6" :step="0.001" /></el-form-item></el-col>
          </el-row>
          <el-row :gutter="12">
            <el-col :span="8"><el-form-item label="线宽 (m)"><el-input-number v-model="form.lineWidth" :min="0.5" :step="1" /></el-form-item></el-col>
            <el-col :span="8"><el-form-item label="线高 (m)"><el-input-number v-model="form.lineHeight" :min="0" :step="0.5" /></el-form-item></el-col>
            <el-col :span="8"><el-form-item label="分段长度 (m)"><el-input-number v-model="form.lineSegmentLength" :min="1" :step="1" /></el-form-item></el-col>
          </el-row>
        </template>

        <el-divider>污染物排放</el-divider>
        <div v-for="(p, idx) in form.pollutants" :key="idx" class="pollutant-row">
          <el-select v-model="p.pollutantType" style="width: 120px">
            <el-option v-for="t in pollutantTypeOptions" :key="t.type" :value="t.type" :label="t.type" />
          </el-select>
          <el-input-number
            v-if="form.sourceType !== 'equivalent_area'"
            v-model="p.emissionRate"
            :min="0"
            :step="0.1"
            placeholder="排放速率 g/s"
            data-test="pollutant-emission-rate-input"
          />
          <el-input-number
            v-if="form.sourceType === 'equivalent_area'"
            v-model="p.concentration"
            :min="0"
            :step="1"
            placeholder="测量浓度 μg/m³"
            data-test="pollutant-concentration-input"
          />
          <el-button type="text" size="small" style="color: #f56c6c" @click="removePollutant(idx)">移除</el-button>
        </div>
        <el-button size="small" plain @click="addPollutant">+ 添加污染物</el-button>

        <el-divider>标记</el-divider>
        <el-row :gutter="12">
          <el-col :span="12"><el-form-item label="标记图标"><el-input v-model="form.markerSymbol" /></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="标记颜色"><el-color-picker v-model="form.markerColor" /></el-form-item></el-col>
        </el-row>
        <el-form-item label="是否启用"><el-switch v-model="form.isActive" /></el-form-item>
      </el-form>
      <span slot="footer">
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="submit">保存</el-button>
      </span>
    </el-dialog>
  </div>
</template>

<script lang="ts">
import Vue from 'vue'
import { sourcesApi } from '@/api'
import { downloadBlob } from '@/utils/download'
import { errorMessage } from '@/utils/error'
import type { EmissionSource, EmissionSourceCreate, PollutantEmissionCreate, PollutantTypeInfo } from '@/types'

type SourceType = 'point' | 'area' | 'equivalent_area' | 'line'

const sourceTypes: Array<{ value: SourceType; label: string }> = [
  { value: 'point', label: '点源' },
  { value: 'area', label: '面源' },
  { value: 'equivalent_area', label: '等效面源' },
  { value: 'line', label: '线源' },
]

const defaultPollutants: PollutantTypeInfo[] = ['PM2.5', 'PM10', 'TSP', 'VOCs', 'NOx', 'O3'].map((type) => ({
  type,
  name: type,
  unit: 'g/s',
  description: '',
}))

function createDefaultForm(): EmissionSourceCreate {
  return {
    name: '',
    sourceType: 'point',
    latitude: 39.9,
    longitude: 116.4,
    height: 50,
    temperature: 400,
    velocity: 15,
    diameter: 2,
    areaLength: 100,
    areaWidth: 100,
    areaHeight: 10,
    areaTemperature: 300,
    startLat: 39.9,
    startLon: 116.4,
    endLat: 39.91,
    endLon: 116.41,
    lineWidth: 10,
    lineHeight: 1,
    lineTemperature: 300,
    lineSegmentLength: 10,
    markerSymbol: 'factory',
    markerColor: '#FF5722',
    isActive: true,
    pollutants: [],
  }
}

export default Vue.extend({
  name: 'SourcesView',
  data: () => ({
    items: [] as EmissionSource[],
    loading: false,
    selected: [] as EmissionSource[],
    sourceTypes,
    pollutantTypes: [] as PollutantTypeInfo[],
    draftFilterType: '' as SourceType | '',
    appliedFilterType: '' as SourceType | '',
    importType: 'point' as SourceType,
    dialogVisible: false,
    dialogMode: 'create' as 'create' | 'edit',
    editId: null as number | null,
    form: createDefaultForm() as EmissionSourceCreate,
  }),
  computed: {
    regionName(): string {
      return this.$store.state.regions.find((r: any) => r.key === this.$store.state.currentRegionKey)?.name || ''
    },
    filteredItems(): EmissionSource[] {
      return this.appliedFilterType ? this.items.filter((i) => i.sourceType === this.appliedFilterType) : this.items
    },
    pollutantTypeOptions(): PollutantTypeInfo[] {
      return this.pollutantTypes.length > 0 ? this.pollutantTypes : defaultPollutants
    },
  },
  watch: {
    '$store.state.currentRegionKey'() {
      this.refresh()
    },
  },
  mounted() {
    this.refresh()
    this.loadMetadata()
  },
  methods: {
    labelOf(t: string) { return sourceTypes.find((x) => x.value === t)?.label || t },
    pollutantValue(row: EmissionSource, pollutant: EmissionSource['pollutants'][number]) {
      return row.sourceType === 'equivalent_area' && pollutant.concentration !== null ? pollutant.concentration : pollutant.emissionRate
    },
    clearSelectedRows() {
      this.selected = []
      ;(this.$refs.tableRef as any)?.clearSelection?.()
    },
    isConfirmDismissed(e: unknown) { return e === 'cancel' || e === 'close' },
    async refresh() {
      this.loading = true
      try {
        this.items = await sourcesApi.list(0, 1000, this.$store.state.currentRegionKey)
        this.clearSelectedRows()
      } catch (e) {
        this.$message.error(errorMessage(e, '加载排放源失败'))
      } finally {
        this.loading = false
      }
    },
    async loadMetadata() {
      try {
        this.pollutantTypes = await sourcesApi.pollutantTypes()
      } catch {
        this.pollutantTypes = defaultPollutants
      }
    },
    resetForm() { this.form = createDefaultForm() },
    openCreate() {
      this.dialogMode = 'create'
      this.editId = null
      this.resetForm()
      this.dialogVisible = true
    },
    openEdit(row: EmissionSource) {
      this.dialogMode = 'edit'
      this.editId = row.id
      this.form = {
        name: row.name,
        sourceType: row.sourceType,
        latitude: row.latitude,
        longitude: row.longitude,
        height: row.height,
        temperature: row.temperature ?? 400,
        velocity: row.velocity ?? 15,
        diameter: row.diameter ?? 2,
        areaLength: row.areaLength ?? 100,
        areaWidth: row.areaWidth ?? 100,
        areaHeight: row.areaHeight ?? 10,
        areaTemperature: row.areaTemperature ?? 300,
        startLat: row.startLat ?? row.latitude,
        startLon: row.startLon ?? row.longitude,
        endLat: row.endLat ?? row.latitude,
        endLon: row.endLon ?? row.longitude,
        lineWidth: row.lineWidth ?? 10,
        lineHeight: row.lineHeight ?? 1,
        lineTemperature: row.lineTemperature ?? 300,
        lineSegmentLength: row.lineSegmentLength ?? 10,
        markerSymbol: row.markerSymbol,
        markerColor: row.markerColor,
        isActive: row.isActive,
        pollutants: row.pollutants.map((p) => ({
          pollutantType: p.pollutantType,
          emissionRate: p.emissionRate,
          concentration: p.concentration,
        })),
      }
      this.dialogVisible = true
    },
    addPollutant() {
      if (!this.form.pollutants) this.$set(this.form, 'pollutants', [])
      this.form.pollutants!.push({ pollutantType: 'PM2.5', emissionRate: 1.0, concentration: null })
    },
    removePollutant(idx: number) { this.form.pollutants!.splice(idx, 1) },
    normalizePayload(): EmissionSourceCreate {
      const payload: EmissionSourceCreate = JSON.parse(JSON.stringify(this.form))
      payload.pollutants = (payload.pollutants ?? []).map((p: PollutantEmissionCreate) => ({
        ...p,
        emissionRate: payload.sourceType === 'equivalent_area' ? 0 : (p.emissionRate ?? 0),
        concentration: payload.sourceType === 'equivalent_area' ? (p.concentration ?? 0) : null,
      }))
      return payload
    },
    async submit() {
      try {
        const payload = this.normalizePayload()
        if (this.dialogMode === 'create') {
          await sourcesApi.create(payload, this.$store.state.currentRegionKey)
          this.$message.success('创建成功')
        } else if (this.editId !== null) {
          await sourcesApi.update(this.editId, payload)
          this.$message.success('更新成功')
        }
        this.dialogVisible = false
        await this.refresh()
      } catch (e) {
        this.$message.error(errorMessage(e, '保存失败'))
      }
    },
    async remove(row: EmissionSource) {
      try {
        await this.$confirm(`确定删除排放源「${row.name}」？相关污染物记录也会被删除`, '删除确认', { type: 'warning' })
        await sourcesApi.delete(row.id)
        this.$message.success('已删除')
        await this.refresh()
      } catch (e) {
        if (this.isConfirmDismissed(e)) return
        this.$message.error(errorMessage(e, '删除失败'))
      }
    },
    async removeSelected() {
      if (this.selected.length === 0) {
        this.$message.warning('请先勾选要删除的排放源')
        return
      }
      try {
        await this.$confirm(`确定删除已选的 ${this.selected.length} 个排放源？相关污染物记录也会被删除`, '批量删除确认', { type: 'warning' })
        const results = await Promise.allSettled(this.selected.map((row) => sourcesApi.delete(row.id)))
        const failed = results.filter((result) => result.status === 'rejected').length
        if (failed > 0) this.$message.error(`批量删除完成，${failed} 个排放源删除失败`)
        else this.$message.success('已批量删除')
        this.clearSelectedRows()
        await this.refresh()
      } catch (e) {
        if (this.isConfirmDismissed(e)) return
        this.$message.error(errorMessage(e, '批量删除失败'))
      }
    },
    async setActive(row: EmissionSource, isActive: boolean) {
      const previous = row.isActive
      row.isActive = isActive
      try {
        await sourcesApi.update(row.id, { isActive })
        this.$message.success(isActive ? '已启用' : '已停用')
        await this.refresh()
      } catch (e) {
        row.isActive = previous
        this.$message.error(errorMessage(e, '更新启用状态失败'))
      }
    },
    async enableAll() {
      const targets = this.items.filter((row) => !row.isActive)
      if (targets.length === 0) {
        this.$message.success('排放源已全部启用')
        return
      }
      const results = await Promise.allSettled(targets.map((row) => sourcesApi.update(row.id, { isActive: true })))
      const failed = results.filter((result) => result.status === 'rejected').length
      if (failed > 0) this.$message.error(`全部启用完成，${failed} 个排放源启用失败`)
      else this.$message.success('排放源已全部启用')
      await this.refresh()
    },
    async disableAll() {
      const targets = this.items.filter((row) => row.isActive)
      if (targets.length === 0) {
        this.$message.success('排放源已全部停用')
        return
      }
      const results = await Promise.allSettled(targets.map((row) => sourcesApi.update(row.id, { isActive: false })))
      const failed = results.filter((result) => result.status === 'rejected').length
      if (failed > 0) this.$message.error(`全部停用完成，${failed} 个排放源停用失败`)
      else this.$message.success('排放源已全部停用')
      await this.refresh()
    },
    applyTypeFilter() {
      this.appliedFilterType = this.draftFilterType
      this.clearSelectedRows()
    },
    async downloadTemplate() {
      try {
        const blob = await sourcesApi.downloadTemplate(this.importType)
        downloadBlob(blob, `${this.importType}_template.xlsx`)
      } catch (e) {
        this.$message.error(errorMessage(e, '下载模板失败'))
      }
    },
    async importFile(file: File) {
      try {
        const res = await sourcesApi.importFile(this.importType, file, this.$store.state.currentRegionKey)
        this.$message.success(res.message)
        await this.refresh()
      } catch (e) {
        this.$message.error(errorMessage(e, '导入失败'))
      }
      return false
    },
  },
})
</script>

<style scoped>
.toolbar{display:flex;align-items:center;gap:8px;margin-bottom:16px;flex-wrap:wrap}.spacer{flex:1}.muted{color:#9ca3af}.pollutant-row{display:flex;gap:8px;align-items:center;margin-bottom:8px}.pollutant-row .el-input-number{width:180px}.el-input-number{width:100%}
</style>
