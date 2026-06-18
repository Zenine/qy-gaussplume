<script setup lang="ts">
import { onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Check, Delete, Edit, Plus, Refresh } from '@element-plus/icons-vue'
import { meteorologyApi } from '@/api'
import { useRegionStore } from '@/stores/region'
import type { Meteorology, MeteorologyCreate } from '@/types'
import { errorMessage } from '@/utils/error'

const regionStore = useRegionStore()

const STABILITY_OPTIONS = ['A', 'B', 'C', 'D', 'E', 'F'] as const

const items = ref<Meteorology[]>([])
const loading = ref(false)
const selected = ref<Meteorology[]>([])
const tableRef = ref<{ clearSelection: () => void }>()

const dialogVisible = ref(false)
const dialogMode = ref<'create' | 'edit'>('create')
const editId = ref<number | null>(null)
const form = reactive<MeteorologyCreate>({
  name: '',
  windSpeed: 2.0,
  windDirection: 0.0,
  boundaryLayerHeight: 1000,
  stabilityClass: 'D',
  temperature: 293.15,
  humidity: 50.0,
  cloudCover: 0.0,
  precipitation: 0.0,
  isActive: true,
})

const formRules = {
  name: [{ required: true, message: '请输入名称', trigger: 'blur' }],
  windSpeed: [{ required: true, type: 'number', message: '请输入风速' }],
  windDirection: [{ required: true, type: 'number', message: '请输入风向' }],
  stabilityClass: [{ required: true, message: '请选择稳定度' }],
}

function clearSelectedRows() {
  selected.value = []
  tableRef.value?.clearSelection()
}

function isConfirmDismissed(e: unknown) {
  return e === 'cancel' || e === 'close'
}

async function refresh() {
  loading.value = true
  try {
    items.value = await meteorologyApi.list(0, 1000, regionStore.currentRegionKey)
    clearSelectedRows()
  } catch (e) {
    ElMessage.error(errorMessage(e, '加载气象场失败'))
  } finally {
    loading.value = false
  }
}

function resetForm() {
  Object.assign(form, {
    name: '',
    windSpeed: 2.0,
    windDirection: 0.0,
    boundaryLayerHeight: 1000,
    stabilityClass: 'D',
    temperature: 293.15,
    humidity: 50.0,
    cloudCover: 0.0,
    precipitation: 0.0,
    isActive: true,
  })
}

function openCreate() {
  dialogMode.value = 'create'
  editId.value = null
  resetForm()
  dialogVisible.value = true
}

function openEdit(row: Meteorology) {
  dialogMode.value = 'edit'
  editId.value = row.id
  Object.assign(form, {
    name: row.name,
    windSpeed: row.windSpeed,
    windDirection: row.windDirection,
    boundaryLayerHeight: row.boundaryLayerHeight ?? 1000,
    stabilityClass: row.stabilityClass ?? 'D',
    temperature: row.temperature ?? 293.15,
    humidity: row.humidity ?? 50.0,
    cloudCover: row.cloudCover ?? 0.0,
    precipitation: row.precipitation ?? 0.0,
    isActive: row.isActive,
  })
  dialogVisible.value = true
}

async function submit() {
  try {
    if (dialogMode.value === 'create') {
      await meteorologyApi.create({ ...form }, regionStore.currentRegionKey)
      ElMessage.success('创建成功')
    } else if (editId.value !== null) {
      await meteorologyApi.update(editId.value, { ...form })
      ElMessage.success('更新成功')
    }
    dialogVisible.value = false
    await refresh()
  } catch (e) {
    ElMessage.error(errorMessage(e, '保存失败'))
  }
}

async function remove(row: Meteorology) {
  try {
    await ElMessageBox.confirm(`确定删除气象场「${row.name}」？`, '删除确认', {
      type: 'warning',
    })
    await meteorologyApi.delete(row.id)
    ElMessage.success('已删除')
    await refresh()
  } catch (e) {
    if (isConfirmDismissed(e)) return
    ElMessage.error(errorMessage(e, '删除失败'))
  }
}

async function removeSelected() {
  if (selected.value.length === 0) {
    ElMessage.warning('请先勾选要删除的气象场')
    return
  }
  try {
    await ElMessageBox.confirm(`确定删除已选的 ${selected.value.length} 个气象场？`, '批量删除确认', {
      type: 'warning',
    })
    const results = await Promise.allSettled(selected.value.map((row) => meteorologyApi.delete(row.id)))
    const failed = results.filter((result) => result.status === 'rejected').length
    if (failed > 0) {
      ElMessage.error(`批量删除完成，${failed} 个气象场删除失败`)
    } else {
      ElMessage.success('已批量删除')
    }
    clearSelectedRows()
    await refresh()
  } catch (e) {
    if (isConfirmDismissed(e)) return
    ElMessage.error(errorMessage(e, '批量删除失败'))
  }
}


async function setActive(row: Meteorology, isActive: boolean) {
  const previous = row.isActive
  row.isActive = isActive
  try {
    await meteorologyApi.update(row.id, { isActive })
    ElMessage.success(isActive ? '已启用' : '已停用')
    await refresh()
  } catch (e) {
    row.isActive = previous
    ElMessage.error(errorMessage(e, '更新启用状态失败'))
  }
}

async function enableAll() {
  const targets = items.value.filter((row) => !row.isActive)
  if (targets.length === 0) {
    ElMessage.success('气象场已全部启用')
    return
  }
  const results = await Promise.allSettled(targets.map((row) => meteorologyApi.update(row.id, { isActive: true })))
  const failed = results.filter((result) => result.status === 'rejected').length
  if (failed > 0) {
    ElMessage.error(`全部启用完成，${failed} 个气象场启用失败`)
  } else {
    ElMessage.success('气象场已全部启用')
  }
  await refresh()
}

onMounted(refresh)
watch(() => regionStore.currentRegionKey, () => { void refresh() })
</script>

<template>
  <div class="table-page meteorology-page">
    <div class="toolbar page-toolbar">
      <el-tag type="primary" effect="plain">{{ regionStore.regions.find((r) => r.key === regionStore.currentRegionKey)?.name }}</el-tag>
      <el-button type="primary" :icon="Plus" @click="openCreate">新增气象场</el-button>
      <el-button type="success" :icon="Check" @click="enableAll">全部启用</el-button>
      <el-button
        type="danger"
        :icon="Delete"
        :disabled="selected.length === 0"
        @click="removeSelected"
      >
        批量删除 ({{ selected.length }})
      </el-button>
      <span class="spacer" />
      <el-button link :icon="Refresh" @click="refresh">刷新</el-button>
    </div>

    <div class="table-shell">
      <el-table
        ref="tableRef"
        v-loading="loading"
        :data="items"
        stripe
        border
        row-key="id"
        @selection-change="(v: Meteorology[]) => (selected = v)"
      >
        <el-table-column type="selection" width="46" />
        <el-table-column prop="id" label="ID" width="60" />
        <el-table-column prop="name" label="名称" min-width="120" />
        <el-table-column label="风速" width="100">
          <template #default="{ row }">{{ row.windSpeed }} m/s</template>
        </el-table-column>
        <el-table-column label="风向" width="100">
          <template #default="{ row }">{{ row.windDirection }}°</template>
        </el-table-column>
        <el-table-column prop="stabilityClass" label="稳定度" width="80" />
        <el-table-column label="边界层高度" width="120">
          <template #default="{ row }">{{ row.boundaryLayerHeight }} m</template>
        </el-table-column>
        <el-table-column label="温度 (K)" width="100">
          <template #default="{ row }">{{ row.temperature }}</template>
        </el-table-column>
        <el-table-column label="湿度 (%)" width="100">
          <template #default="{ row }">{{ row.humidity }}</template>
        </el-table-column>
        <el-table-column label="启用" width="96">
          <template #default="{ row }">
            <el-switch
              v-model="row.isActive"
              :data-test="`meteorology-active-${row.id}`"
              size="small"
              @change="(value: boolean) => setActive(row, value)"
            />
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
      :title="dialogMode === 'create' ? '新增气象场' : '编辑气象场'"
      width="540px"
    >
      <el-form :model="form" :rules="formRules" label-width="120px">
        <el-form-item label="名称" prop="name">
          <el-input v-model="form.name" placeholder="如：冬季北风" />
        </el-form-item>
        <el-form-item label="风速 (m/s)" prop="windSpeed">
          <el-input-number v-model="form.windSpeed" :min="0.1" :step="0.1" />
        </el-form-item>
        <el-form-item label="来风方向 (°)" prop="windDirection">
          <el-input-number v-model="form.windDirection" :min="0" :max="360" :step="1" />
        </el-form-item>
        <el-form-item label="大气稳定度" prop="stabilityClass">
          <el-select v-model="form.stabilityClass">
            <el-option
              v-for="s in STABILITY_OPTIONS"
              :key="s"
              :value="s"
              :label="s"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="边界层高度 (m)">
          <el-input-number v-model="form.boundaryLayerHeight" :min="50" :step="50" />
        </el-form-item>
        <el-form-item label="温度 (K)">
          <el-input-number v-model="form.temperature" :step="0.5" />
        </el-form-item>
        <el-form-item label="湿度 (%)">
          <el-input-number v-model="form.humidity" :min="0" :max="100" :step="1" />
        </el-form-item>
        <el-form-item label="云量 (0-10)">
          <el-input-number v-model="form.cloudCover" :min="0" :max="10" :step="0.5" />
        </el-form-item>
        <el-form-item label="降水 (mm/h)">
          <el-input-number v-model="form.precipitation" :min="0" :step="0.5" />
        </el-form-item>
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
}
.spacer {
  flex: 1;
}
</style>
