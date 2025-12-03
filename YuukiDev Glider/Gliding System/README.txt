# YuukiDev Glider Controller

*By Aldreck Paul L. Obenario — YuukiDev*

A lightweight, modular glider controller for Unity projects. Designed to be drop-in for prototyping or as a packaged module for your game. It groups runtime logic into a single assembly for faster incremental compilation and keeps editor tools separated.

---

## Features
- Smooth gliding movement and input mapping
- Modular design suitable for packaging as a Unity Package
- Single runtime assembly (`YuukiDev.Glider`) and optional Editor assembly
- Simple, easy-to-read C# scripts meant for extension

---

## Requirements
- Unity (recommended: 2021.3 LTS or newer)
- C# 8.0+ (depends on your Unity version)
- No external runtime dependencies by default

> If your project uses different or additional packages, add them to `package.json` or the assembly definition (`.asmdef`) references.

---

## Installation

### Local (during development)
1. Copy the `YuukiDev` folder into your project `Assets/` folder, or
2. Place the package folder into `Packages/` and reference it in `manifest.json` with a `file:` path.

Example `manifest.json` entry (local):
```json
"com.yuukidev.glider": "file:../path/to/YuukiDevGlider"
