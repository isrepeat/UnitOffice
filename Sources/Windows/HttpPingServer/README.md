# HttpPingServer

Для проверки из локальной сети выполни команды ниже **на ПК с сервером** в PowerShell от имени администратора. Здесь IPv4 сервера — `192.168.202.46`, подсеть — `192.168.202.0/24`, активный профиль брандмауэра — `Public`. Если настройки сети отличаются, подставь свои значения и укажи тот же IP при запуске сервера.

## Право на регистрацию URL

`HTTP.sys` проверяет, может ли учётная запись зарегистрировать этот URL. Резервирование позволяет запускать сервер из Visual Studio с обычными правами.

```powershell
netsh http add urlacl url=http://192.168.202.46:8080/ "user=$(whoami)" listen=yes
```

Если PowerShell запущен под другой учётной записью, подставь в `user=` имя пользователя, под которым работает Visual Studio.

## Входящие подключения

Добавь одно правило, разрешающее входящий TCP на порт `8080` только из указанной подсети в профиле `Public`.

```powershell
New-NetFirewallRule -Name "HttpPingServer-LAN-8080" `
  -DisplayName "HttpPingServer LAN TCP 8080" `
  -Direction Inbound -Action Allow -Protocol TCP -LocalPort 8080 `
  -LocalAddress 192.168.202.46 -RemoteAddress 192.168.202.0/24 `
  -Profile Public
```

## Проверка

Запусти `UnitOffice.HttpPingServer` в конфигурации `Debug | x64` и убедись, что в консоли появился адрес `http://192.168.202.46:8080/`. Сначала открой `http://192.168.202.46:8080/test` на ПК с сервером, затем на втором ПК. На странице ожидаются строки `PASS`, а в консоли сервера — `GET /test -> 200` и `POST /ping -> 200`.

## Удаление настроек

Из PowerShell от имени администратора:

```powershell
Remove-NetFirewallRule -Name "HttpPingServer-LAN-8080"
netsh http delete urlacl url=http://192.168.202.46:8080/
```

Если ранее добавлялось правило с тем же отображаемым именем без параметра `-Name`, у него будет другое внутреннее имя. Найди его через `Get-NetFirewallRule -DisplayName "HttpPingServer LAN TCP 8080"`.