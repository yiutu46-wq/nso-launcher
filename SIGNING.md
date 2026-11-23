# Firma de Windows (SmartScreen)

Este repositorio es público y usa el programa open source de **SignPath
Foundation** para firmar el launcher gratis. La firma se hace en GitHub Actions
sobre el artifact que produce el workflow, no en la máquina del maintainer.

Política: todas las releases de Windows se publican firmadas. Los builds de
prueba usan `test-signing`; los tags `v*` usan `release-signing`.

## Cómo activarlo

1. Aplicar al programa open source de SignPath:
   <https://signpath.org/open-source>
   Repositorio: `https://github.com/yiutu46-wq/nso-launcher`
   Licencia: MIT (`LICENSE`).
2. Cuando aprueben, crear en SignPath:
   - Proyecto: `nso-launcher`.
   - Artifact configuration: `launcher` (subir `signpath/launcher.xml`; firma el `.exe` dentro del ZIP del artifact).
   - Signing policies: `test-signing` y `release-signing`.
3. En GitHub → Settings → Secrets and variables → Actions:
   - Secret `SIGNPATH_API_TOKEN`: token de un usuario con permiso de submitter.
   - Variable `SIGNPATH_ORGANIZATION_ID`: ID de la organización en SignPath.
4. Ejecutar el workflow a mano para probar `test-signing`.
5. Para una release: crear y subir un tag `v1.0.6` (o el que corresponda).
   El workflow firma, verifica con `signtool` y publica el `.exe` y
   `SHA256SUMS.txt` en GitHub Releases.

## Verificar

```powershell
Get-AuthenticodeSignature .\NinjaSagaPvP-Launcher.exe | Format-List Status, SignerCertificate
signtool verify /pa /v .\NinjaSagaPvP-Launcher.exe
```

## Notas

- SignPath Foundation firma con una identidad de editor compartida
  (“SignPath Foundation”), usada por cientos de proyectos open source. Eso da
  reputación en SmartScreen sin comprar un certificado EV.
- Si más adelante se quiere que figure el nombre del proyecto como editor, hay
  que comprar un certificado OV/EV propio y firmar con un PFX en Secrets.
- Nunca subir tokens, certificados ni claves al repositorio.
