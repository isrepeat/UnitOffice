R"WEB(<!doctype html>
<html lang="ru">
<head>
	<meta charset="utf-8">
	<meta name="viewport" content="width=device-width">
	<title>Локальный ↔ СЕДО</title>
	<link rel="stylesheet" href="/web/chat.css">
</head>
<body>
	<h1>Локальный ↔ СЕДО</h1>
	<form id="connect">
		<label>
			Этот компьютер
			<select id="sender">
				<option value="local">Локальный</option>
				<option value="sedo">СЕДО</option>
			</select>
		</label>
		<label>
			Код доступа
			<input id="key" type="password" required autocomplete="off">
		</label>
		<button>Подключиться</button>
	</form>
	<p id="status">Введите общий код доступа.</p>
	<div id="messages" role="log"></div>
	<form id="send">
		<textarea id="text" maxlength="4000" required placeholder="Сообщение"></textarea>
		<button id="submit" disabled>Отправить</button>
	</form>
	<script src="/web/chat.js"></script>
</body>
</html>
)WEB"