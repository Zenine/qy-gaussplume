# 快速开始

QY-GaussPlume 可以在本机用一个脚本启动完整前后端环境，适合先体验主控台模拟、数据管理和贡献分析，再进入二次开发。

## 三步上手

1. 准备 .NET SDK 9.0.x 和 Node.js 20+。
2. 克隆仓库并安装前端依赖。
3. 运行启动脚本并打开浏览器。

```bash
git clone git@github.com:Zenine/qy-gaussplume.git
cd qy-gaussplume
cd frontend-vue && npm install --registry=https://registry.npmmirror.com && cd ..
./scripts/start.sh
```

## 一句话模板

如果你想让 AI 助手继续开发这个项目，可以使用：

```text
进入 QY-GaussPlume 仓库根目录后，请读 `QUICK_START.md`，然后向我提问。没有问题就开始工作。
```

## 验证

```bash
./scripts/verify.sh
```

该命令会执行后端 xUnit、前端 Vitest 和前端生产构建。

## 中断后恢复

```text
请读 checkpoint.md，继续上次未完成的工作。
```
