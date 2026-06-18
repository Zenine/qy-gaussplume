using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GnnSimulation.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillDefaultRegion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO regions (key, name, sort_order)
                SELECT 'nanhu', '南湖区', 1
                WHERE NOT EXISTS (SELECT 1 FROM regions WHERE key = 'nanhu');
                """);
            migrationBuilder.Sql("""
                INSERT INTO regions (key, name, sort_order)
                SELECT 'xiuzhou', '秀洲区', 2
                WHERE NOT EXISTS (SELECT 1 FROM regions WHERE key = 'xiuzhou');
                """);
            migrationBuilder.Sql("""
                INSERT INTO regions (key, name, sort_order)
                SELECT 'jiashan', '嘉善县', 3
                WHERE NOT EXISTS (SELECT 1 FROM regions WHERE key = 'jiashan');
                """);
            migrationBuilder.Sql("""
                INSERT INTO regions (key, name, sort_order)
                SELECT 'tongxiang', '桐乡市', 4
                WHERE NOT EXISTS (SELECT 1 FROM regions WHERE key = 'tongxiang');
                """);

            migrationBuilder.Sql("""
                INSERT INTO region_sources (region_id, source_id)
                SELECT r.id, s.id
                FROM emission_sources s
                JOIN regions r ON r.key = 'nanhu'
                WHERE NOT EXISTS (
                    SELECT 1 FROM region_sources rs WHERE rs.source_id = s.id
                );
                """);
            migrationBuilder.Sql("""
                INSERT INTO region_receptors (region_id, receptor_id)
                SELECT r.id, rec.id
                FROM receptors rec
                JOIN regions r ON r.key = 'nanhu'
                WHERE NOT EXISTS (
                    SELECT 1 FROM region_receptors rr WHERE rr.receptor_id = rec.id
                );
                """);
            migrationBuilder.Sql("""
                INSERT INTO region_meteorology (region_id, meteorology_id)
                SELECT r.id, m.id
                FROM meteorology m
                JOIN regions r ON r.key = 'nanhu'
                WHERE NOT EXISTS (
                    SELECT 1 FROM region_meteorology rm WHERE rm.meteorology_id = m.id
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 数据回填不做反向删除，避免回滚迁移时误删用户后续建立的区域归属。
        }
    }
}
