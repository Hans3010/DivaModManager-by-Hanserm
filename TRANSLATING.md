# Translating DivaModManager by Hanserm

*(Español más abajo)*

DivaModManager loads extra languages from the `Languages` folder next to `DivaModManager.exe`.
English and Japanese are built in; every other language is a folder you can add without programming.

## Add a language

1. Copy `Languages/_template` and rename the copy to your language code, for example `Languages/pt` or `Languages/fr`.
   Folders starting with `_` are ignored, and `en` / `jp` are reserved.
2. Edit the three files inside:
   - **`language.toml`**: `name` is what appears in the language selectors (for example `"Português"`), `author` is you.
   - **`ui.toml`**: buttons, tabs, check boxes and tooltips. Each line is `"English text" = "translation"`.
     Only change the text on the **right**; the left side must stay exactly as it is.
   - **`messages.toml`**: the message windows. Translate the text in quotes and keep `[WINDOW-xxxx]`,
     the field names (`info`, `context`, `window_title`, `button_1`...), the `{0}` `{1}` placeholders and `\n` line breaks.
3. Save the files as **UTF-8**.
4. Start DivaModManager and choose your language in **Option → Language / Idioma** (or in the first-launch window).

Anything you leave untranslated (or delete) is shown in English, so you can translate little by little.

## Share it

Open a pull request adding your `Languages/<code>` folder to
[Hans3010/DivaModManager-by-Hanserm](https://github.com/Hans3010/DivaModManager-by-Hanserm), or attach the folder to an issue.

## Notes

- Some texts stay in English on purpose: the mod list column headers and a few drop-downs are used by the code itself.
- Messages in the log at the bottom of the window are not translated yet.

---

# Traducir DivaModManager by Hanserm

DivaModManager carga idiomas extra desde la carpeta `Languages`, al lado de `DivaModManager.exe`.
El inglés y el japonés vienen incluidos; cualquier otro idioma es una carpeta que puedes agregar sin programar.

## Agregar un idioma

1. Copia `Languages/_template` y renombra la copia con el código de tu idioma, por ejemplo `Languages/pt` o `Languages/fr`.
   Las carpetas que empiezan con `_` se ignoran, y `en` / `jp` están reservados.
2. Edita los tres archivos:
   - **`language.toml`**: `name` es lo que aparece en los selectores de idioma (por ejemplo `"Português"`) y `author` eres tú.
   - **`ui.toml`**: botones, pestañas, casillas y tooltips. Cada línea es `"texto en inglés" = "traducción"`.
     Cambia solo el texto de la **derecha**; el de la izquierda tiene que quedar exactamente igual.
   - **`messages.toml`**: las ventanas de aviso. Traduce el texto entre comillas y respeta `[WINDOW-xxxx]`,
     los nombres de los campos (`info`, `context`, `window_title`, `button_1`...), los `{0}` `{1}` y los `\n`.
3. Guarda los archivos en **UTF-8**.
4. Abre DivaModManager y elige tu idioma en **Option → Language / Idioma** (o en la ventana del primer inicio).

Lo que no traduzcas (o borres) se muestra en inglés, así que puedes ir traduciendo de a poco.

## Compartirlo

Abre un pull request agregando tu carpeta `Languages/<código>` a
[Hans3010/DivaModManager-by-Hanserm](https://github.com/Hans3010/DivaModManager-by-Hanserm), o adjunta la carpeta en un issue.
