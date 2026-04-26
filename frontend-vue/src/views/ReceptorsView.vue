<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox, type UploadRawFile } from 'element-plus'
import { receptorsApi } from '@/api'
import type { Receptor, ReceptorCreate } from '@/types'
import { downloadBlob } from '@/utils/download'
import { errorMessage } from '@/utils/error'

const items = ref<Receptor[]>([])
const loading = ref(false)
const selected = ref<Receptor[]>([])

const dialogVisible = ref(false)
const dialogMode = ref<'create' | 'edit'>('create')
const editId = ref<number | null>(null)
const form = reactive<ReceptorCreate>({
  name: '',
  latitude: 39.9,
  longitude: 116.4,
  height: 1.5,
  markerSymbol: 'monitor',
  markerColor: '#2196F3',
  isActive: true,
})

const formRules = {
  name: [{ required: true, message: '请输入名称', trigger: 'blur' }],
  latitude: [{ required: true, type: 'number', message: '请输入纬度' }],
  longitude: [{ required: true, type: 'number', message: '请输入经度' }],
}

async function refresh() {
  loading.value = true
  try {
    items.value = await receptorsApi.list(0, 1000)
  } catch (e) {
    ElMessage.error(errorMessage(e, '加载受体点失败'))
  } finally {
    loading.value = false
  }
}

function openCreate() {
  dialogMode.value = 'create'
  editId.value = null
  Object.assign(form, {
    name: '',
    latitude: 39.9,
    longitude: 116.4,
    height: 1.5,
    markerSymbol: 'monitor',
    markerColor: '#2196F3',
    isActive: true,
  })
  dialogVisible.value = true
}

function openEdit(row: Receptor) {
  dialogMode.value = 'edit'
  editId.value = row.id
  Object.assign(form, {
    name: row.name,
    latitude: row.latitude,
    longitude: row.longitude,
    height: row.height,
    markerSymbol: row.markerSymbol,
    markerColor: row.markerColor,
    isActive: row.isActive,
  })
  dialogVisible.value = true
}

async function submit() {
  try {
    if (dialogMode.value === 'create') {
      await receptorsApi.create({ ...form })
      ElMessage.success('创建成功')
    } else if (editId.value !== null) {
      await receptorsApi.update(editId.value, { ...form })
      ElMessage.success('更新成功')
    }
    dialogVisible.value = false
    await refresh()
  } catch (e) {
    ElMessage.error(errorMessage(e, '保存失败'))
  }
}

async function remove(row: Receptor) {
  try {
    await ElMessageBox.confirm(`确定删除受体点「${row.name}」？`, '删除确认', {
      type: 'warning',
    })
    await receptorsApi.delete(row.id)
    ElMessage.success('已删除')
    await refresh()
  } catch (e) {
    if (e === 'cancel') return
    ElMessage.error(errorMessage(e, '删除失败'))
  }
}

async function downloadTemplate() {
  try {
    const blob = await receptorsApi.downloadTemplate()
    downloadBlob(blob, 'receptors_template.xlsx')
  } catch (e) {
    ElMessage.error(errorMessage(e, '下载模板失败'))
  }
}

async function importFile(file: UploadRawFile) {
  try {
    const res = await receptorsApi.importFile(file as unknown as File)
    ElMessage.success(res.message)
    await refresh()
  } catch (e) {
    ElMessage.error(errorMessage(e, '导入失败'))
  }
  return false // 阻止 el-upload 默认行为
}

async function exportSelected() {
  if (selected.value.length === 0) {
    ElMessage.warning('请先勾选要导出的受体点')
    return
  }
  try {
    const blob = await receptorsApi.export(selected.value.map((x) => x.id))
    downloadBlob(blob, 'receptors_export.xlsx')
  } catch (e) {
    ElMessage.error(errorMessage(e, '导出失败'))
  }
}

onMounted(refresh)
</script>

<template>
  <div>
    <div class="toolbar">
      <el-button type="primary" @click="openCreate">新增受体点</el-button>
      <el-button @click="downloadTemplate">下载模板</el-button>
      <el-upload
        :auto-upload="true"
        :show-file-list="false"
        accept=".xlsx,.xls"
        :before-upload="importFile"
      >
        <el-button>导入 Excel</el-button>
      </el-upload>
      <el-button :disabled="selected.length === 0" @click="exportSelected">
        导出已选 ({{ selected.length }})
      </el-button>
      <span class="spacer" />
      <el-button link @click="refresh">刷新</el-button>
    </div>

    <el-table
      v-loading="loading"
      :data="items"
      stripe
      border
      row-key="id"
      @selection-change="(v: Receptor[]) => (selected = v)"
    >
      <el-table-column type="selection" width="46" />
      <el-table-column prop="id" label="ID" width="60" />
      <el-table-column prop="name" label="名称" min-width="140" />
      <el-table-column prop="latitude" label="纬度" width="120" />
      <el-table-column prop="longitude" label="经度" width="120" />
      <el-table-column prop="height" label="高度 (m)" width="100" />
      <el-table-column label="标记" width="120">
        <template #default="{ row }">
          <el-tag :color="row.markerColor" effect="dark">{{ row.markerSymbol }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="isActive" label="启用" width="80">
        <template #default="{ row }">
          <el-tag :type="row.isActive ? 'success' : 'info'">
            {{ row.isActive ? '是' : '否' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="140" fixed="right">
        <template #default="{ row }">
          <el-button size="small" link @click="openEdit(row)">编辑</el-button>
          <el-button size="small" link type="danger" @click="remove(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog
      v-model="dialogVisible"
      :title="dialogMode === 'create' ? '新增受体点' : '编辑受体点'"
      width="480px"
    >
      <el-form :model="form" :rules="formRules" label-width="100px">
        <el-form-item label="名称" prop="name">
          <el-input v-model="form.name" placeholder="请输入名称" />
        </el-form-item>
        <el-form-item label="纬度" prop="latitude">
          <el-input-number v-model="form.latitude" :precision="6" :step="0.001" />
        </el-form-item>
        <el-form-item label="经度" prop="longitude">
          <el-input-number v-model="form.longitude" :precision="6" :step="0.001" />
        </el-form-item>
        <el-form-item label="高度 (m)">
          <el-input-number v-model="form.height" :min="0" :step="0.5" />
        </el-form-item>
        <el-form-item label="标记图标">
          <el-input v-model="form.markerSymbol" />
        </el-form-item>
        <el-form-item label="标记颜色">
          <el-color-picker v-model="form.markerColor" />
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
  flex-wrap: wrap;
}
.spacer {
  flex: 1;
}
</style>
