# Preparar una release de BrowserSwitch

Este procedimiento es para quien mantiene el proyecto. Quien descarga el ZIP solo necesita extraerlo, ejecutar BrowserSwitch.exe y pulsar Instalar BrowserSwitch.

## Versionar y preparar el código

1. Mantén `AssemblyFileVersion` en `src/BrowserSwitch/Properties/AssemblyInfo.cs` con cuatro componentes; el paquete usa los tres primeros. La primera versión es `1.0.0.0` → `1.0.0`.
2. Crea las notas `docs/releases/vX.Y.Z.md` y actualiza los enlaces/versiones de la portada.
3. Confirma los cambios y súbelos a `origin/main`. El empaquetador exige un directorio de trabajo limpio.

```powershell
.\scripts\Package.ps1
```

El script ejecuta la batería local, compila con los recursos del proyecto y crea:

```text
artifacts/releases/
├── BrowserSwitch-X.Y.Z-windows.zip
├── SHA256SUMS.txt
└── package-validation.json
```

El ZIP solo contiene una lista explícita de archivos del programa, instrucciones, versión, notas y scripts opcionales de desinstalación. Nunca se recogen la carpeta Data, preferencias, credenciales, accesos directos ni archivos personales del equipo.

Antes de darlo por válido, se extrae en una carpeta nueva con espacios, se comparan todos los archivos y hashes, se revisa FileVersion y se ejecuta `--diagnostics` desde el binario extraído. El manifiesto interno identifica el commit y los hashes de los archivos. Los directorios de preparación y prueba se conservan bajo artifacts para inspección, fuera de Git.

## Publicar

La cuenta debe tener permiso de escritura en el repositorio y Git debe estar autenticado con su gestor de credenciales habitual.

```powershell
.\scripts\Publish-Release.ps1
```

El publicador verifica que el ZIP corresponde al commit limpio actual y que ese commit ya está en origin/main. Crea o comprueba una etiqueta anotada `vX.Y.Z`, sin reescribir etiquetas existentes ni usar force.

Después crea un borrador en GitHub, sube el ZIP y SHA256SUMS.txt, y comprueba tamaños y hashes devueltos por GitHub. Solo entonces publica la release y verifica una descarga real del ZIP mediante la API autenticada. Un intento interrumpido puede reutilizar el borrador si los archivos son idénticos; los archivos distintos o una release ya publicada se conservan y hacen que el script se detenga.

Las credenciales permanecen en memoria. No se escriben tokens en archivos, argumentos de procesos ni salida del script. El resultado no sensible se guarda en `artifacts/releases/published-release.json`.

La visibilidad del repositorio no se cambia. Si es privado, también se necesita acceso para descargar sus releases.

## Alcance de la comprobación

La prueba de extracción y diagnóstico no sustituye una instalación limpia en otro Windows. Antes de ampliar la distribución conviene comprobar instalación, actualización, inicio de sesión, Teams y desinstalación en un equipo de prueba. La aplicación requiere .NET Framework 4.8 y el ejecutable todavía no tiene firma digital.