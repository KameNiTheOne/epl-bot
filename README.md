# epl-bot
Easy Peasy Lawyer main repository

Setup:
1. Устанавливаем git.
2. Создаём рабочую папку и проект.
Открываем папку в Visual Studio Code, заходим в расширения:
![Безыsgsgмянный](https://github.com/user-attachments/assets/0e6867e5-2a1b-4183-9886-7b7cd20ecc0c)

качаем C# Dev Kit:

![Безыsgsgмянный](https://github.com/user-attachments/assets/c965a634-3b31-426c-815f-b69c7c3034ac)

Ctrl+Shift+P -> .NET: New Project -> Console.App -> Вписываем название -> т.д до завершения.

В файлах проекта должны появиться 3 новые папки, одна из которых папка с вписанным названием:

![{2BA4FBE3-DB7E-45DF-9A5A-A5DFAD0383E5}](https://github.com/user-attachments/assets/6ecc399c-361b-446f-94c4-255da502a6ce)

Удаляем файл Program.cs из этой папки.

3.  git pull-аем этот репозиторий в эту папку:

![Безыsgsgмянный](https://github.com/user-attachments/assets/22bcd6c8-aee7-4817-b9a3-939d0c6d293c)

git init

![{FF6AFD23-6097-4AAA-A1BF-8C8CAA04D2AD}](https://github.com/user-attachments/assets/fc404cf3-8149-4ac3-ac52-15c57cfeda74)

git pull https://github.com/KameNiTheOne/epl-bot

![Безыsgsgмянный](https://github.com/user-attachments/assets/fc60c85c-3e6e-40e7-bb38-fa41c1051f69)

Ctrl+Shift+P -> NuGet: Add NuGet Package -> Устанавливаем(Если просит) .NET SDK 8 с чем-то версии -> Newtonsoft.json, LlamaSharp, LLamaSharp.Backend.CPU, LLamaSharp.Backend.Cuda12, LLamaSharp.Backend.Vulkan, Telegram.Bot (всё последних версий)

После установки пакетов должны пропасть критические ошибки.

Заменяем путь к конфиг-файлу в Program.cs на путь к конфиг-файлу в проекте и путь к модели в TheGPT.cs на путь скачанной модели. Готово!
