# Spanish Memes for MX Keypad

Un botón para reproducir un sonido aleatorio de las tendencias de Myinstants en España desde el MX Creative Keypad, en macOS.

## Instalación

Requiere macOS, conexión a internet, Logi Options+ con Logi Plugin Service 6.4 o posterior y un dispositivo compatible.

1. Descarga `SpanishMemes_<versión>.lplug4` de la [última release](https://github.com/robertohuertasm/spanish-memes-logi-plugin/releases/latest).
2. Abre el archivo para instalarlo con Logi Options+.
3. En Options+, arrastra **Spanish Memes → Meme aleatorio** a una tecla.

Pulsa para reproducir. Mientras suena, las pulsaciones adicionales se ignoran. Mantén pulsada la tecla para detenerlo.

Si personalizaste el icono de la tecla anteriormente, restablece el icono predeterminado en el editor de iconos de Options+ para ver el del plugin.

[Instrucciones web](https://robertohuertasm.github.io/spanish-memes-logi-plugin/).

## Cómo funciona

El plugin obtiene la primera página de [tendencias de Myinstants en España](https://www.myinstants.com/en/index/es/), guarda los enlaces durante una hora y descarga un audio al azar la primera vez que se reproduce; después lo sirve desde `~/Library/Caches/SpanishMemesKeypad`. Si la web responde 429, 403 o 503, pausa las peticiones según `Retry-After` (una hora por defecto) y usa la caché mientras tanto. Utiliza `afplay` y la salida de audio del Mac. No incluye sonidos.

La integración lee la página web, no la API; cambios en la página o bloqueos de red pueden impedir su funcionamiento.

## Desarrollo y releases

Requiere .NET 10 y el paquete LogiPluginApi. Para comprobar y empaquetar:

```sh
dotnet run --project probes/Probe.csproj -- --behavior
dotnet build src/SpanishMemesPlugin.csproj -c Release
logiplugintool pack build/Release SpanishMemes_0.1.3.lplug4
logiplugintool verify SpanishMemes_0.1.3.lplug4
```

Actualiza la versión de `package/metadata/LoupedeckPackage.yaml` y sube una etiqueta coincidente, por ejemplo `v0.1.3`. El flujo de release comprueba el comportamiento, compila, empaqueta y publica el archivo `.lplug4`.

Los diagnósticos están en `~/Library/Application Support/Logi/LogiPluginService/Logs/plugin_logs/SpanishMemes.log`.

## Licencia

Código e icono: MIT. Los sonidos pertenecen a sus respectivos titulares.
