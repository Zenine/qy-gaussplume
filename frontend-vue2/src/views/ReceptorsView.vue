<template>
  <div class="table-page receptors-page">
    <div class="toolbar page-toolbar">
      <el-tag type="primary" effect="plain">{{ regionName }}</el-tag>
      <el-button type="primary" icon="el-icon-plus" @click="openCreate">新增受体点</el-button>
      <el-button icon="el-icon-download" @click="downloadTemplate">下载模板</el-button>
      <el-upload :auto-upload="true" :show-file-list="false" accept=".xlsx,.xls" :before-upload="importFile">
        <el-button icon="el-icon-upload2">批量导入</el-button>
      </el-upload>
      <el-button icon="el-icon-download" :disabled="selected.length === 0" @click="exportSelected">
        导出已选 ({{ selected.length }})
      </el-button>
      <el-button type="success" icon="el-icon-check" @click="enableAll">全部启用</el-button>
      <el-button type="warning" icon="el-icon-close" @click="disableAll">全部停用</el-button>
      <el-button type="danger" icon="el-icon-delete" :disabled="selected.length === 0" @click="removeSelected">
        批量删除 ({{ selected.length }})
      </el-button>
      <span class="spacer" />
      <el-button type="text" icon="el-icon-refresh" @click="refresh">刷新</el-button>
    </div>

    <div class="table-shell">
      <el-table
        ref="tableRef"
        v-loading="loading"
        :data="items"
        border
        stripe
        row-key="id"
        @selection-change="selected = $event"
      >
        <el-table-column type="selection" width="46" />
        <el-table-column prop="id" label="ID" width="60" />
        <el-table-column prop="name" label="名称" min-width="140" />
        <el-table-column prop="latitude" label="纬度" width="150" show-overflow-tooltip />
        <el-table-column prop="longitude" label="经度" width="150" show-overflow-tooltip />
        <el-table-column prop="height" label="高度 (m)" width="100" />
        <el-table-column label="标记" width="120">
          <template slot-scope="scope"><el-tag :color="scope.row.markerColor" effect="dark">{{ scope.row.markerSymbol }}</el-tag></template>
        </el-table-column>
        <el-table-column label="启用" width="96">
          <template slot-scope="scope">
            <el-switch
              v-model="scope.row.isActive"
              :data-test="`receptor-active-${scope.row.id}`"
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

    <el-dialog :visible.sync="dialogVisible" :title="dialogMode === 'create' ? '新增受体点' : '编辑受体点'" width="520px">
      <el-form :model="form" label-width="104px">
        <el-form-item label="名称" required><el-input v-model="form.name" placeholder="请输入名称" /></el-form-item>
        <el-form-item label="纬度" required><el-input-number v-model="form.latitude" :precision="6" :step="0.001" /></el-form-item>
        <el-form-item label="经度" required><el-input-number v-model="form.longitude" :precision="6" :step="0.001" /></el-form-item>
        <el-form-item label="高度 (m)"><el-input-number v-model="form.height" :min="0" :step="0.5" /></el-form-item>
        <el-form-item label="标记图标"><el-input v-model="form.markerSymbol" /></el-form-item>
        <el-form-item label="标记颜色"><el-color-picker v-model="form.markerColor" /></el-form-item>
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
import { receptorsApi } from '@/api'
import { downloadBlob } from '@/utils/download'
import { errorMessage } from '@/utils/error'
import type { Receptor, ReceptorCreate } from '@/types'

function createDefaultForm(): ReceptorCreate {
  return {
    name: '',
    latitude: 39.9,
    longitude: 116.4,
    height: 1.5,
    markerSymbol: 'monitor',
    markerColor: '#2196F3',
    isActive: true,
  }
}

export default Vue.extend({
  name: 'ReceptorsView',
  data: () => ({
    items: [] as Receptor[],
    loading: false,
    selected: [] as Receptor[],
    dialogVisible: false,
    dialogMode: 'create' as 'create' | 'edit',
    editId: null as number | null,
    form: createDefaultForm() as ReceptorCreate,
  }),
  computed: {
    regionName(): string {
      return this.$store.state.regions.find((r: any) => r.key === this.$store.state.currentRegionKey)?.name || ''
    },
  },
  watch: {
    '$store.state.currentRegionKey'() { this.refresh() },
  },
  mounted() { this.refresh() },
  methods: {
    clearSelectedRows() {
      this.selected = []
      ;(this.$refs.tableRef as any)?.clearSelection?.()
    },
    isConfirmDismissed(e: unknown) { return e === 'cancel' || e === 'close' },
    async refresh() {
      this.loading = true
      try {
        this.items = await receptorsApi.list(0, 1000, this.$store.state.currentRegionKey)
        this.clearSelectedRows()
      } catch (e) {
        this.$message.error(errorMessage(e, '加载受体点失败'))
      } finally {
        this.loading = false
      }
    },
    openCreate() {
      this.dialogMode = 'create'
      this.editId = null
      this.form = createDefaultForm()
      this.dialogVisible = true
    },
    openEdit(row: Receptor) {
      this.dialogMode = 'edit'
      this.editId = row.id
      this.form = {
        name: row.name,
        latitude: row.latitude,
        longitude: row.longitude,
        height: row.height,
        markerSymbol: row.markerSymbol,
        markerColor: row.markerColor,
        isActive: row.isActive,
      }
      this.dialogVisible = true
    },
    async submit() {
      try {
        if (this.dialogMode === 'create') {
          await receptorsApi.create({ ...this.form }, this.$store.state.currentRegionKey)
          this.$message.success('创建成功')
        } else if (this.editId !== null) {
          await receptorsApi.update(this.editId, { ...this.form })
          this.$message.success('更新成功')
        }
        this.dialogVisible = false
        await this.refresh()
      } catch (e) {
        this.$message.error(errorMessage(e, '保存失败'))
      }
    },
    async remove(row: Receptor) {
      try {
        await this.$confirm(`确定删除受体点「${row.name}」？`, '删除确认', { type: 'warning' })
        await receptorsApi.delete(row.id)
        this.$message.success('已删除')
        await this.refresh()
      } catch (e) {
        if (this.isConfirmDismissed(e)) return
        this.$message.error(errorMessage(e, '删除失败'))
      }
    },
    async removeSelected() {
      if (this.selected.length === 0) {
        this.$message.warning('请先勾选要删除的受体点')
        return
      }
      try {
        await this.$confirm(`确定删除已选的 ${this.selected.length} 个受体点？`, '批量删除确认', { type: 'warning' })
        const results = await Promise.allSettled(this.selected.map((row) => receptorsApi.delete(row.id)))
        const failed = results.filter((result) => result.status === 'rejected').length
        if (failed > 0) this.$message.error(`批量删除完成，${failed} 个受体点删除失败`)
        else this.$message.success('已批量删除')
        this.clearSelectedRows()
        await this.refresh()
      } catch (e) {
        if (this.isConfirmDismissed(e)) return
        this.$message.error(errorMessage(e, '批量删除失败'))
      }
    },
    async setActive(row: Receptor, isActive: boolean) {
      const previous = row.isActive
      row.isActive = isActive
      try {
        await receptorsApi.update(row.id, { isActive })
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
        this.$message.success('受体点已全部启用')
        return
      }
      const results = await Promise.allSettled(targets.map((row) => receptorsApi.update(row.id, { isActive: true })))
      const failed = results.filter((result) => result.status === 'rejected').length
      if (failed > 0) this.$message.error(`全部启用完成，${failed} 个受体点启用失败`)
      else this.$message.success('受体点已全部启用')
      await this.refresh()
    },
    async disableAll() {
      const targets = this.items.filter((row) => row.isActive)
      if (targets.length === 0) {
        this.$message.success('受体点已全部停用')
        return
      }
      const results = await Promise.allSettled(targets.map((row) => receptorsApi.update(row.id, { isActive: false })))
      const failed = results.filter((result) => result.status === 'rejected').length
      if (failed > 0) this.$message.error(`全部停用完成，${failed} 个受体点停用失败`)
      else this.$message.success('受体点已全部停用')
      await this.refresh()
    },
    async downloadTemplate() {
      try {
        const blob = await receptorsApi.downloadTemplate()
        downloadBlob(blob, 'receptors_template.xlsx')
      } catch (e) {
        this.$message.error(errorMessage(e, '下载模板失败'))
      }
    },
    async importFile(file: File) {
      try {
        const res = await receptorsApi.importFile(file, this.$store.state.currentRegionKey)
        this.$message.success(res.message)
        await this.refresh()
      } catch (e) {
        this.$message.error(errorMessage(e, '导入失败'))
      }
      return false
    },
    async exportSelected() {
      if (this.selected.length === 0) {
        this.$message.warning('请先勾选要导出的受体点')
        return
      }
      try {
        const blob = await receptorsApi.export(this.selected.map((x) => x.id))
        downloadBlob(blob, 'receptors_export.xlsx')
      } catch (e) {
        this.$message.error(errorMessage(e, '导出失败'))
      }
    },
  },
})
</script>

<style scoped>
.toolbar{display:flex;align-items:center;gap:8px;margin-bottom:16px;flex-wrap:wrap}.spacer{flex:1}.el-input-number{width:100%}
</style>
