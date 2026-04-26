<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Delete, Edit, Plus, Refresh } from '@element-plus/icons-vue'
import { meteorologyApi } from '@/api'
import type { Meteorology, MeteorologyCreate } from '@/types'
import { errorMessage } from '@/utils/error'

const STABILITY_OPTIONS = ['A', 'B', 'C', 'D', 'E', 'F'] as const

const items = ref<Meteorology[]>([])
const loading = ref(false)

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
})

const formRules = {
  name: [{ required: true, message: '请输入名称', trigger: 'blur' }],
  windSpeed: [{ required: true, type: 'number', message: '请输入风速' }],
  windDirection: [{ required: true, type: 'number', message: '请输入风向' }],
  stabilityClass: [{ required: true, message: '请选择稳定度' }],
}

async function refresh() {
  loading.value = true
  try {
    items.value = await meteorologyApi.list(0, 1000)
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
  })
  dialogVisible.value = true
}

async function submit() {
  try {
    if (dialogMode.value === 'create') {
      await meteorologyApi.create({ ...form })
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
    if (e === 'cancel') return
    ElMessage.error(errorMessage(e, '删除失败'))
  }
}

onMounted(refresh)
</script>

<template>
  <div class="table-page meteorology-page">
    <div class="toolbar page-toolbar">
      <el-button type="primary" :icon="Plus" @click="openCreate">新增气象场</el-button>
      <span class="spacer" />
      <el-button link :icon="Refresh" @click="refresh">刷新</el-button>
    </div>

    <div class="table-shell">
      <el-table v-loading="loading" :data="items" stripe border row-key="id">
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
        <el-form-item label="风向 (°)" prop="windDirection">
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
