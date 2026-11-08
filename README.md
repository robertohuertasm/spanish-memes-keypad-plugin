# Spanish Memes for MX Keypad

Un botón para reproducir un sonido aleatorio de las tendencias de Myinstants en España desde el MX Creative Keypad, en macOS.

## Instalación

Requiere macOS, conexión a internet, Logi Options+ con Logi Plugin Service 6.4 o posterior y un dispositivo compatible.

1. Descarga `SpanishMemes_<versión>.lplug4` de la [última release](https://github.com/robertohuertasm/spanish-memes-keypad-plugin/releases/latest). El repositorio es privado: necesitas acceso a él.
2. Abre el archivo para instalarlo con Logi Options+.
3. En Options+, arrastra **Spanish Memes → Meme aleatorio** a una tecla.

Pulsa para reproducir. Mientras suena, las pulsaciones adicionales se ignoran. Mantén pulsada la tecla para detenerlo.

[Instrucciones web](https://robertohuertasm.github.io/spanish-memes-keypad-plugin/).

## Cómo funciona

El plugin obtiene la primera página de [tendencias de Myinstants en España](https://www.myinstants.com/en/index/es/), guarda los enlaces en memoria durante una hora y descarga un audio al azar en cada reproducción. Utiliza `afplay` y la salida de audio del Mac. No incluye sonidos.

La integración lee la página web, no la API; cambios en la página o bloqueos de red pueden impedir su funcionamiento. Las [condiciones de Myinstants](https://www.myinstants.com/en/terms_of_use.html) restringen las peticiones automatizadas; hay que aclarar el acceso con el proveedor antes de distribuir o utilizar esta integración.

## Desarrollo y releases

Requiere .NET 10 y el paquete LogiPluginApi. Para comprobar y empaquetar:

```sh
dotnet run --project probes/Probe.csproj -- --behavior
dotnet build src/SpanishMemesPlugin.csproj -c Release
logiplugintool pack build/Release SpanishMemes_0.1.2.lplug4
logiplugintool verify SpanishMemes_0.1.2.lplug4
```

Actualiza la versión de `package/metadata/LoupedeckPackage.yaml` y sube una etiqueta coincidente, por ejemplo `v0.1.2`. El flujo de release comprueba el comportamiento, compila, empaqueta y publica el archivo `.lplug4`.

Los diagnósticos están en `~/Library/Application Support/Logi/LogiPluginService/Logs/plugin_logs/SpanishMemes.log`.

## Licencia

Código e icono: MIT. Los sonidos pertenecen a sus respectivos titulares.
