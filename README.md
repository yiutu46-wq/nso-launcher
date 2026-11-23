# Ninja Saga PvP — Launcher

Launcher de Windows para [Ninja Saga PvP](https://ninja-saga-pvp.vercel.app).
Descarga el juego, verifica el SHA-256, lo extrae con 7-Zip y aplica el parche
que deja el splash de Adobe AIR en 1×1.

Este repositorio contiene **solo el launcher**. El cliente y el servidor del
juego no están acá.

## Compilar

Requisitos:

- Windows con .NET Framework 4.x (`csc.exe`).
- Python 3 con Pillow.
- 7-Zip instalado (`C:\Program Files\7-Zip`).

```powershell
python -m pip install pillow
pwsh .\build.ps1
```

El resultado queda en `dist/NinjaSagaPvP-Launcher.exe`.

`build.ps1` baja las imágenes de marca desde
`https://nso.173-249-31-27.sslip.io/brand/` y verifica su SHA-256 antes de
compilar.

## Firma

Las releases se firman con **SignPath Foundation** desde GitHub Actions para
evitar el aviso de SmartScreen. Ver [SIGNING.md](SIGNING.md) y
`.github/workflows/windows-launcher.yml`.

## Descargar

La versión publicada está en <https://ninja-saga-pvp.vercel.app>.

## Licencia

MIT para el código del launcher. Las imágenes de marca y el nombre Ninja Saga
pertenecen a sus dueños.
