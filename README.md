# SysBot.NET
![Licencia](https://img.shields.io/badge/License-AGPLv3-blue.svg)

## Disclaimer
Este fork está pensado para mi uso personal. Eres libre de usar este fork, pero ten en cuenta que no estoy dispuesto a añadir características específicas que no se ajusten a mis necesidades.

## Support Discords:

Para soporte específico para este fork del fork de ForkBot del repo SysBot.NET de kwsch, ¡siéntete libre de unirte! **(No se dará soporte en el Discord oficial de PKHeX o PA, por favor no molestes a los desarrolladores)**

[<img src="https://discordapp.com/api/guilds/1079448118933852160/widget.png?style=banner2">](https://discord.gg/Ny6XND5B8R)

Esta fork utiliza código de:
* [Berichan](https://github.com/berichan)'s [SysBot.PokemonScarletViolet](https://github.com/berichan/SysBot.PokemonScarletViolet)
* [Koi](https://github.com/Koi-3088)'s [ForkBot.NET](https://github.com/Koi-3088/ForkBot.NET)
* [Zyro](https://github.com/zyro670)'s [NotForkBot.NET](https://github.com/zyro670/NotForkBot.NET)
* [Manu098vm](https://github.com/Manu098vm)'s [SysBot.NET](https://github.com/Manu098vm/SysBot.NET)

Gracias a todos los desarrolladores y colaboradores de los forks mencionados.

[usb-Botbase](https://github.com/Koi-3088/USB-Botbase) cliente de control remoto USB para este fork.

[sys-botbase](https://github.com/olliz0r/sys-botbase) cliente para la automatización por control remoto de las consolas Nintendo Switch.

## SysBot.Base:
- Biblioteca lógica de base para proyectos específicos de juegos.
- Contiene una clase de conexión Bot síncrona y asíncrona para interactuar con sys-botbase.

## SysBot.Tests:
- Pruebas unitarias para garantizar que la lógica se comporta según lo previsto :)

# Ejemplos de aplicación

La fuerza impulsora para desarrollar este proyecto son los bots automatizados para los juegos Pokémon de Nintendo Switch. Se proporciona un ejemplo de implementación en este repo para demostrar tareas interesantes que este framework es capaz de realizar. Consulta la [Wiki](https://github.com/kwsch/SysBot.NET/wiki) para más detalles sobre las funciones Pokémon soportadas.

## SysBot.Pokemon:
- Librería de clases usando SysBot.Base para contener lógica relacionada con la creación y ejecución de bots Sword/Shield.

## SysBot.Pokemon.WinForms:
- Simple GUI Launcher para añadir, iniciar y detener bots Pokémon (como se ha descrito anteriormente).
- La configuración de los ajustes del programa se realiza in-app y se guarda como un archivo json local.
  
## SysBot.Pokemon.Discord:
- Interfaz Discord para interactuar remotamente con la GUI WinForms.
- Proporcione un token de inicio de sesión de discordia y los roles que se les permite interactuar con sus robots.
- Se proporcionan comandos para gestionar y unirse a la cola de distribución.

## SysBot.Pokemon.Twitch:
- Interfaz de Twitch.tv para anunciar a distancia el inicio de la distribución.
- Proporciona un token de inicio de sesión de Twitch, un nombre de usuario y un canal para iniciar sesión.

## SysBot.Pokemon.YouTube:
- Interfaz de YouTube.com para anunciar a distancia el inicio de la distribución.
- Proporcione un ClientID, ClientSecret y ChannelID de inicio de sesión de YouTube.
  
Usa [Discord.Net](https://github.com/discord-net/Discord.Net) , [TwitchLib](https://github.com/TwitchLib/TwitchLib) y [StreamingClientLibary](https://github.com/SaviorXTanren/StreamingClientLibrary) como dependencia a través de Nuget.

## Otras dependencias
La lógica de la API Pokémon la proporciona [PKHeX](https://github.com/kwsch/PKHeX/), y la generación de plantillas se realiza mediante [AutoMod](https://github.com/architdate/PKHeX-Plugins/).

## Créditos especiales:
Este fork de SysBot es una aglomeración de diferentes forks y otros programas basados en PKHeX/SysBot.
Gracias a [Santacrab](https://github.com/santacrab2), [Zyro](https://github.com/zyro670), [Koi](https://github.com/Koi-3088), [Berichan](https://github.com/berichan), [Anubis](https://github.com/Lusamine), [Archit](https://github.com/architdate), [Kurt](https://github.com/kwsch) y a todos los desarrolladores que han contribuido y contribuirán a sus repositorios.

# Licencia
Consulte el archivo `License.md` para obtener más información sobre las licencias.
