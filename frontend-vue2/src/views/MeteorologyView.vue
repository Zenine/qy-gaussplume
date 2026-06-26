<template>
  <div class="table-page meteorology-page">
    <div class="toolbar page-toolbar">
      <el-tag type="primary" effect="plain">{{ regionName }}</el-tag>
      <el-button type="primary" icon="el-icon-plus" @click="openCreate">新增气象场</el-button>
      <el-button type="success" icon="el-icon-check" @click="enableAll">全部启用</el-button>
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
        <el-table-column prop="name" label="名称" min-width="130" />
        <el-table-column label="风速" width="100"><template slot-scope="scope">{{ scope.row.windSpeed }} m/s</template></el-table-column>
        <el-table-column label="来风方向" width="100"><template slot-scope="scope">{{ scope.row.windDirection }}°</template></el-table-column>
        <el-table-column prop="stabilityClass" label="稳定度" width="80" />
        <el-table-column label="边界层高度" width="120"><template slot-scope="scope">{{ scope.row.boundaryLayerHeight }} m</template></el-table-column>
        <el-table-column label="温度 (K)" width="100"><template slot-scope="scope">{{ scope.row.temperature }}</template></el-table-column>
        <el-table-column label="湿度 (%)" width="100"><template slot-scope="scope">{{ scope.row.humidity }}</template></el-table-column>
        <el-table-column label="云量" width="90"><template slot-scope="scope">{{ scope.row.cloudCover }}</template></el-table-column>
        <el-table-column label="降水" width="90"><template slot-scope="scope">{{ scope.row.precipitation }}</template></el-table-column>
        <el-table-column label="启用" width="96">
          <template slot-scope="scope">
            <el-switch
              v-model="scope.row.isActive"
              :data-test="`meteorology-active-${scope.row.id}`"
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

    <el-dialog :visible.sync="dialogVisible" :title="dialogMode === 'create' ? '新增气象场' : '编辑气象场'" width="560px">
      <el-form :model="form" label-width="124px">
        <el-form-item label="名称" required><el-input v-model="form.name" placeholder="如：冬季北风" /></el-form-item>
        <el-form-item label="风速 (m/s)" required><el-input-number v-model="form.windSpeed" :min="0.1" :step="0.1" /></el-form-item>
        <el-form-item label="来风方向 (°)" required><el-input-number v-model="form.windDirection" :min="0" :max="360" :step="1" /></el-form-item>
        <el-form-item label="大气稳定度" required>
          <el-select v-model="form.stabilityClass">
            <el-option v-for="s in stabilityOptions" :key="s" :value="s" :label="s" />
          </el-select>
        </el-form-item>
        <el-form-item label="边界层高度 (m)"><el-input-number v-model="form.boundaryLayerHeight" :min="50" :step="50" /></el-form-item>
        <el-form-item label="温度 (K)"><el-input-number v-model="form.temperature" :step="0.5" /></el-form-item>
        <el-form-item label="湿度 (%)"><el-input-number v-model="form.humidity" :min="0" :max="100" :step="1" /></el-form-item>
        <el-form-item label="云量 (0-10)"><el-input-number v-model="form.cloudCover" :min="0" :max="10" :step="0.5" /></el-form-item>
        <el-form-item label="降水 (mm/h)"><el-input-number v-model="form.precipitation" :min="0" :step="0.5" /></el-form-item>
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
import { meteorologyApi } from '@/api'
import { errorMessage } from '@/utils/error'
import type { Meteorology, MeteorologyCreate } from '@/types'

function createDefaultForm(): MeteorologyCreate {
  return {
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
  }
}

export default Vue.extend({
  name: 'MeteorologyView',
  data: () => ({
    items: [] as Meteorology[],
    loading: false,
    selected: [] as Meteorology[],
    stabilityOptions: ['A', 'B', 'C', 'D', 'E', 'F'],
    dialogVisible: false,
    dialogMode: 'create' as 'create' | 'edit',
    editId: null as number | null,
    form: createDefaultForm() as MeteorologyCreate,
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
        this.items = await meteorologyApi.list(0, 1000, this.$store.state.currentRegionKey)
        this.clearSelectedRows()
      } catch (e) {
        this.$message.error(errorMessage(e, '加载气象场失败'))
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
    openEdit(row: Meteorology) {
      this.dialogMode = 'edit'
      this.editId = row.id
      this.form = {
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
      }
      this.dialogVisible = true
    },
    async submit() {
      try {
        if (this.dialogMode === 'create') {
          await meteorologyApi.create({ ...this.form }, this.$store.state.currentRegionKey)
          this.$message.success('创建成功')
        } else if (this.editId !== null) {
          await meteorologyApi.update(this.editId, { ...this.form })
          this.$message.success('更新成功')
        }
        this.dialogVisible = false
        await this.refresh()
      } catch (e) {
        this.$message.error(errorMessage(e, '保存失败'))
      }
    },
    async remove(row: Meteorology) {
      try {
        await this.$confirm(`确定删除气象场「${row.name}」？`, '删除确认', { type: 'warning' })
        await meteorologyApi.delete(row.id)
        this.$message.success('已删除')
        await this.refresh()
      } catch (e) {
        if (this.isConfirmDismissed(e)) return
        this.$message.error(errorMessage(e, '删除失败'))
      }
    },
    async removeSelected() {
      if (this.selected.length === 0) {
        this.$message.warning('请先勾选要删除的气象场')
        return
      }
      try {
        await this.$confirm(`确定删除已选的 ${this.selected.length} 个气象场？`, '批量删除确认', { type: 'warning' })
        const results = await Promise.allSettled(this.selected.map((row) => meteorologyApi.delete(row.id)))
        const failed = results.filter((result) => result.status === 'rejected').length
        if (failed > 0) this.$message.error(`批量删除完成，${failed} 个气象场删除失败`)
        else this.$message.success('已批量删除')
        this.clearSelectedRows()
        await this.refresh()
      } catch (e) {
        if (this.isConfirmDismissed(e)) return
        this.$message.error(errorMessage(e, '批量删除失败'))
      }
    },
    async setActive(row: Meteorology, isActive: boolean) {
      const previous = row.isActive
      row.isActive = isActive
      try {
        await meteorologyApi.update(row.id, { isActive })
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
        this.$message.success('气象场已全部启用')
        return
      }
      const results = await Promise.allSettled(targets.map((row) => meteorologyApi.update(row.id, { isActive: true })))
      const failed = results.filter((result) => result.status === 'rejected').length
      if (failed > 0) this.$message.error(`全部启用完成，${failed} 个气象场启用失败`)
      else this.$message.success('气象场已全部启用')
      await this.refresh()
    },
  },
})
</script>

<style scoped>
.toolbar{display:flex;align-items:center;gap:8px;margin-bottom:16px;flex-wrap:wrap}.spacer{flex:1}.el-input-number{width:100%}.el-select{width:100%}
</style>
