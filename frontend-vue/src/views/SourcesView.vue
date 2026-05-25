<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox, type UploadRawFile } from 'element-plus'
import { Delete, Download, Edit, Plus, Refresh, Upload } from '@element-plus/icons-vue'
import { sourcesApi } from '@/api'
import type {
  EmissionSource,
  EmissionSourceCreate,
  PollutantEmissionCreate,
  PollutantTypeInfo,
} from '@/types'
import { downloadBlob } from '@/utils/download'
import { errorMessage } from '@/utils/error'

type SourceType = 'point' | 'area' | 'equivalent_area' | 'line'

const SOURCE_TYPES: Array<{ value: SourceType; label: string }> = [
  { value: 'point', label: '点源' },
  { value: 'area', label: '面源' },
  { value: 'equivalent_area', label: '等效面源' },
  { value: 'line', label: '线源' },
]

function labelOf(t: string) {
  return SOURCE_TYPES.find((x) => x.value === t)?.label ?? t
}

const items = ref<EmissionSource[]>([])
const loading = ref(false)
const pollutantTypes = ref<PollutantTypeInfo[]>([])
const filterType = ref<SourceType | ''>('')

const dialogVisible = ref(false)
const dialogMode = ref<'create' | 'edit'>('create')
const editId = ref<number | null>(null)
const importType = ref<SourceType>('point')

const form = reactive<EmissionSourceCreate>({
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
})

const formRules = {
  name: [{ required: true, message: '请输入名称', trigger: 'blur' }],
  latitude: [{ required: true, type: 'number', message: '请输入纬度' }],
  longitude: [{ required: true, type: 'number', message: '请输入经度' }],
}

async function refresh() {
  loading.value = true
  try {
    items.value = await sourcesApi.list(0, 1000)
  } catch (e) {
    ElMessage.error(errorMessage(e, '加载排放源失败'))
  } finally {
    loading.value = false
  }
}

async function loadMetadata() {
  try {
    pollutantTypes.value = await sourcesApi.pollutantTypes()
  } catch {
    // 非关键，忽略
  }
}

function resetForm() {
  Object.assign(form, {
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
    pollutants: [] as PollutantEmissionCreate[],
  })
}

function openCreate() {
  dialogMode.value = 'create'
  editId.value = null
  resetForm()
  dialogVisible.value = true
}

function openEdit(row: EmissionSource) {
  dialogMode.value = 'edit'
  editId.value = row.id
  Object.assign(form, {
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
  })
  dialogVisible.value = true
}

function addPollutant() {
  form.pollutants!.push({ pollutantType: 'PM2.5', emissionRate: 1.0, concentration: null })
}
function removePollutant(idx: number) {
  form.pollutants!.splice(idx, 1)
}

function pollutantValue(row: EmissionSource, pollutant: EmissionSource['pollutants'][number]) {
  return row.sourceType === 'equivalent_area' && pollutant.concentration !== null
    ? pollutant.concentration
    : pollutant.emissionRate
}

async function submit() {
  try {
    const payload: EmissionSourceCreate = JSON.parse(JSON.stringify(form))
    payload.pollutants = (payload.pollutants ?? []).map((p) => ({
      ...p,
      emissionRate: payload.sourceType === 'equivalent_area' ? 0 : (p.emissionRate ?? 0),
      concentration: payload.sourceType === 'equivalent_area' ? (p.concentration ?? 0) : null,
    }))
    if (dialogMode.value === 'create') {
      await sourcesApi.create(payload)
      ElMessage.success('创建成功')
    } else if (editId.value !== null) {
      await sourcesApi.update(editId.value, payload)
      ElMessage.success('更新成功')
    }
    dialogVisible.value = false
    await refresh()
  } catch (e) {
    ElMessage.error(errorMessage(e, '保存失败'))
  }
}

async function remove(row: EmissionSource) {
  try {
    await ElMessageBox.confirm(`确定删除排放源「${row.name}」？相关污染物记录也会被删除`, '删除确认', {
      type: 'warning',
    })
    await sourcesApi.delete(row.id)
    ElMessage.success('已删除')
    await refresh()
  } catch (e) {
    if (e === 'cancel') return
    ElMessage.error(errorMessage(e, '删除失败'))
  }
}

async function downloadTemplate() {
  try {
    const blob = await sourcesApi.downloadTemplate(importType.value)
    downloadBlob(blob, `${importType.value}_template.xlsx`)
  } catch (e) {
    ElMessage.error(errorMessage(e, '下载模板失败'))
  }
}

async function importFile(file: UploadRawFile) {
  try {
    const res = await sourcesApi.importFile(importType.value, file as unknown as File)
    ElMessage.success(res.message)
    await refresh()
  } catch (e) {
    ElMessage.error(errorMessage(e, '导入失败'))
  }
  return false
}

function filteredItems() {
  if (!filterType.value) return items.value
  return items.value.filter((x) => x.sourceType === filterType.value)
}

onMounted(() => {
  refresh()
  loadMetadata()
})
</script>

<template>
  <div class="table-page sources-page">
    <div class="toolbar page-toolbar">
      <el-button type="primary" :icon="Plus" @click="openCreate">新增排放源</el-button>
      <el-divider direction="vertical" />
      <span>类型：</span>
      <el-select v-model="importType" style="width: 120px">
        <el-option
          v-for="t in SOURCE_TYPES"
          :key="t.value"
          :value="t.value"
          :label="t.label"
        />
      </el-select>
      <el-button :icon="Download" @click="downloadTemplate">下载模板</el-button>
      <el-upload
        :auto-upload="true"
        :show-file-list="false"
        accept=".xlsx,.xls"
        :before-upload="importFile"
      >
        <el-button :icon="Upload">批量导入</el-button>
      </el-upload>
      <span class="spacer" />
      <span>过滤：</span>
      <el-select v-model="filterType" style="width: 140px" clearable>
        <el-option
          v-for="t in SOURCE_TYPES"
          :key="t.value"
          :value="t.value"
          :label="t.label"
        />
      </el-select>
      <el-button link :icon="Refresh" @click="refresh">刷新</el-button>
    </div>

    <div class="table-shell">
      <el-table v-loading="loading" :data="filteredItems()" stripe border row-key="id">
        <el-table-column prop="id" label="ID" width="60" />
        <el-table-column prop="name" label="名称" min-width="140" />
        <el-table-column label="类型" width="90">
          <template #default="{ row }">
            <el-tag size="small">{{ labelOf(row.sourceType) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="latitude" label="纬度" width="132" show-overflow-tooltip />
        <el-table-column prop="longitude" label="经度" width="132" show-overflow-tooltip />
        <el-table-column label="高度 (m)" width="100">
          <template #default="{ row }">{{ row.height }}</template>
        </el-table-column>
        <el-table-column label="污染物" min-width="160">
          <template #default="{ row }">
            <el-tag
              v-for="p in row.pollutants"
              :key="p.id"
              size="small"
              style="margin-right: 4px"
            >
              {{ p.pollutantType }}: {{ pollutantValue(row, p) }}
            </el-tag>
            <span v-if="row.pollutants.length === 0" class="muted">—</span>
          </template>
        </el-table-column>
        <el-table-column label="启用" width="70">
          <template #default="{ row }">
            <el-tag :type="row.isActive ? 'success' : 'info'" size="small">
              {{ row.isActive ? '是' : '否' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="140" fixed="right">
          <template #default="{ row }">
            <el-button size="small" link :icon="Edit" @click="openEdit(row)">编辑</el-button>
            <el-button size="small" link type="danger" :icon="Delete" @click="remove(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-dialog
      v-model="dialogVisible"
      :title="dialogMode === 'create' ? '新增排放源' : '编辑排放源'"
      width="720px"
    >
      <el-form :model="form" :rules="formRules" label-width="110px">
        <el-form-item label="名称" prop="name">
          <el-input v-model="form.name" placeholder="请输入名称" />
        </el-form-item>
        <el-form-item label="类型">
          <el-radio-group v-model="form.sourceType">
            <el-radio-button
              v-for="t in SOURCE_TYPES"
              :key="t.value"
              :value="t.value"
            >{{ t.label }}</el-radio-button>
          </el-radio-group>
        </el-form-item>
        <el-row :gutter="12">
          <el-col :span="12">
            <el-form-item label="纬度" prop="latitude">
              <el-input-number v-model="form.latitude" :precision="6" :step="0.001" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="经度" prop="longitude">
              <el-input-number v-model="form.longitude" :precision="6" :step="0.001" />
            </el-form-item>
          </el-col>
        </el-row>

        <!-- 点源字段 -->
        <template v-if="form.sourceType === 'point'">
          <el-row :gutter="12">
            <el-col :span="8">
              <el-form-item label="高度 (m)">
                <el-input-number v-model="form.height" :min="0" :step="1" />
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="烟气温度 (K)">
                <el-input-number v-model="form.temperature" :min="200" :step="10" />
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="出口速度 (m/s)">
                <el-input-number v-model="form.velocity" :min="0" :step="1" />
              </el-form-item>
            </el-col>
          </el-row>
          <el-form-item label="烟囱直径 (m)">
            <el-input-number v-model="form.diameter" :min="0.1" :step="0.1" />
          </el-form-item>
        </template>

        <!-- 面源字段 -->
        <template v-if="form.sourceType === 'area' || form.sourceType === 'equivalent_area'">
          <el-row :gutter="12">
            <el-col :span="8">
              <el-form-item label="长度 (m)">
                <el-input-number v-model="form.areaLength" :min="1" :step="10" />
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="宽度 (m)">
                <el-input-number v-model="form.areaWidth" :min="1" :step="10" />
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="高度 (m)">
                <el-input-number v-model="form.areaHeight" :min="0" :step="1" />
              </el-form-item>
            </el-col>
          </el-row>
          <el-form-item label="面源温度 (K)">
            <el-input-number v-model="form.areaTemperature" :min="200" :step="10" />
          </el-form-item>
        </template>

        <!-- 线源字段 -->
        <template v-if="form.sourceType === 'line'">
          <el-row :gutter="12">
            <el-col :span="12">
              <el-form-item label="起点纬度">
                <el-input-number v-model="form.startLat" :precision="6" :step="0.001" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="起点经度">
                <el-input-number v-model="form.startLon" :precision="6" :step="0.001" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="终点纬度">
                <el-input-number v-model="form.endLat" :precision="6" :step="0.001" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="终点经度">
                <el-input-number v-model="form.endLon" :precision="6" :step="0.001" />
              </el-form-item>
            </el-col>
          </el-row>
          <el-row :gutter="12">
            <el-col :span="8">
              <el-form-item label="线宽 (m)">
                <el-input-number v-model="form.lineWidth" :min="0.5" :step="1" />
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="线高 (m)">
                <el-input-number v-model="form.lineHeight" :min="0" :step="0.5" />
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="分段长度 (m)">
                <el-input-number v-model="form.lineSegmentLength" :min="1" :step="1" />
              </el-form-item>
            </el-col>
          </el-row>
        </template>

        <!-- 污染物子表 -->
        <el-divider>污染物排放</el-divider>
        <div
          v-for="(p, idx) in form.pollutants"
          :key="idx"
          class="pollutant-row"
        >
          <el-select v-model="p.pollutantType" style="width: 110px">
            <el-option
              v-for="t in pollutantTypes"
              :key="t.type"
              :value="t.type"
              :label="t.type"
            />
          </el-select>
          <el-input-number
            v-if="form.sourceType !== 'equivalent_area'"
            v-model="p.emissionRate"
            :min="0"
            :step="0.1"
            placeholder="排放速率 g/s"
          />
          <el-input-number
            v-if="form.sourceType === 'equivalent_area'"
            v-model="p.concentration"
            :min="0"
            :step="1"
            placeholder="测量浓度 μg/m³"
          />
          <el-button size="small" link type="danger" @click="removePollutant(idx)">
            移除
          </el-button>
        </div>
        <el-button size="small" plain @click="addPollutant">+ 添加污染物</el-button>

        <el-divider>标记</el-divider>
        <el-row :gutter="12">
          <el-col :span="12">
            <el-form-item label="标记图标">
              <el-input v-model="form.markerSymbol" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="标记颜色">
              <el-color-picker v-model="form.markerColor" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="是否启用">
          <el-switch v-model="form.isActive" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="submit">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.toolbar {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 16px;
  flex-wrap: wrap;
}
.spacer {
  flex: 1;
}
.muted {
  color: #9ca3af;
}
.pollutant-row {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 8px;
}
</style>
