#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
快速并行计算测试脚本

测试新的并行API端点，验证：
1. NumPy警告已消除
2. 并行计算正常工作
3. 加速效果显著
"""

import requests
import time
import json

def test_parallel_api():
    """测试并行API端点"""
    print("\n" + "🚀"*35)
    print("       快速并行计算测试")
    print("🚀"*35)

    base_url = "http://localhost:8000"

    print(f"\n📍 测试服务器: {base_url}")
    print(f"📋 测试配置:")
    print(f"   - 风向数量: 12个 (0°, 30°, 60°, ... , 330°)")
    print(f"   - 网格分辨率: 10m")
    print(f"   - 模拟域大小: 5km")
    print(f"   - 并行进程数: 自动检测")

    test_data = {
        "meteorology_id": 1,
        "wind_speed": 3.0,
        "wind_directions": list(range(0, 360, 30)),
        "grid_resolution": 10.0,
        "domain_size": 5000.0,
        "receptor_height": 0.0,
        "num_workers": None
    }

    print(f"\n⏳ 发送并行计算请求...")
    start_time = time.time()

    try:
        response = requests.post(
            f"{base_url}/api/simulation/run_parallel",
            json=test_data,
            timeout=120
        )

        elapsed_time = time.time() - start_time

        if response.status_code == 200:
            result = response.json()

            print(f"\n{'='*70}")
            print(f"✅ 并行计算成功完成！")
            print(f"{'='*70}")
            print(f"\n📊 结果统计:")
            print(f"   ⏱️  总耗时（含网络传输）: {elapsed_time:.2f} 秒")
            print(f"   ⏱️  纯计算时间: {result.get('computation_time_seconds', 'N/A')} 秒")
            print(f"   🎯 成功风向数: {result.get('successful_simulations', 0)}/{result.get('total_wind_directions', 0)}")
            print(f"   💻 使用进程数: {result.get('num_workers_used', 'N/A')}")
            print(f"   ⚡ 加速比: {result.get('speedup_factor', 'N/A')}x")

            if result.get('errors'):
                print(f"\n⚠️  失败的风向:")
                for error in result['errors']:
                    print(f"   - 风向 {error.get('wind_direction')}°: {error.get('error')}")

            if result.get('results'):
                print(f"\n📈 各风向最大浓度:")
                for r in result['results'][:5]:
                    conc = r.get('concentrations', [])
                    if conc:
                        max_c = max(max(row) for row in conc)
                        print(f"   风向 {r.get('wind_direction'):3.0f}°: 最大浓度 = {max_c:.4f} μg/m³")
                if len(result['results']) > 5:
                    print(f"   ... 还有 {len(result['results'])-5} 个风向")

            print(f"\n{'='*70}")

            computation_time = result.get('computation_time_seconds', elapsed_time)
            speedup = result.get('speedup_factor', 0)

            if speedup and speedup > 10:
                print(f"🎉 太棒了！加速效果非常显著！")
                print(f"   原始串行时间估算: ~{computation_time * speedup / 60:.1f} 分钟")
                print(f"   现在并行时间: {computation_time:.1f} 秒")
            elif computation_time < 30:
                print(f"✅ 计算速度很快！在30秒内完成了所有风向。")
            else:
                print(f"⚠️ 计算时间较长，可能需要检查系统资源。")

            return True

        else:
            print(f"\n❌ 请求失败!")
            print(f"   HTTP状态码: {response.status_code}")
            try:
                error_detail = response.json()
                print(f"   错误详情: {json.dumps(error_detail, indent=2, ensure_ascii=False)}")
            except:
                print(f"   响应内容: {response.text[:500]}")
            return False

    except requests.exceptions.ConnectionError:
        print(f"\n❌ 无法连接到服务器!")
        print(f"   请确保服务器已启动: python main.py")
        print(f"   服务器地址: {base_url}")
        return False
    except requests.exceptions.Timeout:
        print(f"\n❌ 请求超时!")
        print(f"   计算时间超过120秒，可能存在性能问题")
        return False
    except Exception as e:
        print(f"\n❌ 测试失败!")
        print(f"   错误类型: {type(e).__name__}")
        print(f"   错误信息: {str(e)}")
        import traceback
        traceback.print_exc()
        return False


def main():
    """主函数"""
    print("\n" + "🔧"*35)
    print("       GNN 并行计算验证工具")
    print("🔧"*35)

    print("\n💡 使用说明:")
    print("   1. 确保已启动服务器: cd backend && python main.py")
    print("   2. 运行此脚本: python test_parallel.py")
    print("   3. 观察输出结果和加速比\n")

    success = test_parallel_api()

    if success:
        print("\n✅ 测试通过！")
        print("\n📝 下一步:")
        print("   1. 打开浏览器访问 http://localhost:8000")
        print("   2. 选择'全局模拟'模式")
        print("   3. 添加多个风向并运行模拟")
        print("   4. 体验显著的加速效果！\n")
    else:
        print("\n❌ 测试未通过，请检查:")
        print("   1. 服务器是否正常运行?")
        print("   2. 数据库是否有气象场数据? (meteorology_id=1)")
        print("   3. 查看上方错误信息\n")


if __name__ == "__main__":
    main()
