# Сквозная сетка в PersonalEventsBuilder

Сквозная сетка позволяет нескольким представлениям показывать одни и те же логические колонки или строки. Например, форма может показывать колонки 1–15, а более узкая таблица под ней — 1–13. После прокрутки вправо видимые диапазоны станут 2–16 и 2–14.

## Из чего состоит сетка

| Компонент | Назначение |
| --- | --- |
| `GridCanvas` | Рисует линии, вычисляет число целых ячеек, помещающихся на поверхности, и обрабатывает перетаскивание. |
| `GridLayout` | Объединяет две оси — `Columns` и `Rows`. |
| `GridAxis` | Хранит шаг `Step` в логических пикселях WinUI, количество ячеек `Count` и индекс первой видимой ячейки `FirstVisibleIndex` (от нуля). |

Связь задаётся ссылкой на один экземпляр оси. Перетаскивание вертикальной линии меняет шаг колонок, горизонтальной — шаг строк. Поверхности, использующие изменившуюся ось, получают уведомление и заново рассчитывают видимую часть. Одинаковые значения у двух отдельно созданных осей позволяют начать с одинакового шага и дальше менять его независимо.

В примерах используются пространства имён `xmlns:controls="using:PersonalEventsBuilder.Controls"` и `xmlns:views="using:PersonalEventsBuilder.Views"`. Общие ресурсы объявляются в `WorkspaceHost.Resources` перед деревом зон. Рабочий пример расположен в [WorkspacePage.xaml](../../Sources/Windows/PersonalEventsBuilder/Pages/WorkspacePage.xaml).

## 1. Одна независимая сетка

Обе оси можно создать прямо там, где размещён контрол. Для двух независимых сеток такое описание размещается в каждой из них.

```xml
<controls:GridCanvas>
    <controls:GridCanvas.GridLayout>
        <controls:GridLayout>
            <controls:GridLayout.Columns>
                <controls:GridAxis Step="116"
                                   Count="24" />
            </controls:GridLayout.Columns>
            <controls:GridLayout.Rows>
                <controls:GridAxis Step="40"
                                   Count="6" />
            </controls:GridLayout.Rows>
        </controls:GridLayout>
    </controls:GridCanvas.GridLayout>
</controls:GridCanvas>
```

Получится 24 колонки шириной 116 и 6 строк высотой 40. Уменьшение зоны сокращает количество видимых ячеек. Шаг и логический размер сетки сохраняются.

## 2. Общие колонки для представлений друг под другом

У формы и таблицы могут различаться количество строк, высота строки и ширина поверхности. Для согласования колонок объявите одну ось и передайте её обеим раскладкам.

```xml
<controls:GridAxis x:Key="EventColumns"
                   Step="116"
                   Count="24" />

<controls:GridLayout x:Key="FormGrid"
                     Columns="{StaticResource EventColumns}">
    <controls:GridLayout.Rows>
        <controls:GridAxis Step="40"
                           Count="6" />
    </controls:GridLayout.Rows>
</controls:GridLayout>

<controls:GridLayout x:Key="CandidatesGrid"
                     Columns="{StaticResource EventColumns}">
    <controls:GridLayout.Rows>
        <controls:GridAxis Step="28"
                           Count="30" />
    </controls:GridLayout.Rows>
</controls:GridLayout>
```

В соответствующих зонах разместите представления:

```xml
<views:EventForm GridLayout="{StaticResource FormGrid}" />
```

```xml
<views:CandidatesTable GridLayout="{StaticResource CandidatesGrid}" />
```

Горизонтальная прокрутка сдвигает обе сетки. Изменение высоты строки или вертикальная прокрутка внутри таблицы затрагивает только таблицу. Между связанными зонами может находиться сводка или другое представление.

## 3. Общие строки для представлений рядом

Пусть слева показаны имена, а справа — сведения по тем же записям. Обеим раскладкам нужна одна ось строк. Колонки каждая определяет самостоятельно.

```xml
<controls:GridAxis x:Key="CandidateRows"
                   Step="28"
                   Count="30" />

<controls:GridLayout x:Key="NamesGrid"
                     Rows="{StaticResource CandidateRows}">
    <controls:GridLayout.Columns>
        <controls:GridAxis Step="180"
                           Count="1" />
    </controls:GridLayout.Columns>
</controls:GridLayout>

<controls:GridLayout x:Key="DetailsGrid"
                     Rows="{StaticResource CandidateRows}">
    <controls:GridLayout.Columns>
        <controls:GridAxis Step="90"
                           Count="8" />
    </controls:GridLayout.Columns>
</controls:GridLayout>
```

Каждому `GridCanvas` передайте свою раскладку через `GridLayout="{StaticResource NamesGrid}"` или `GridLayout="{StaticResource DetailsGrid}"`. Для визуального совпадения горизонтальных линий выровняйте верхние края поверхностей с учётом высоты заголовков.

## 4. Два представления одной координатной области

Чтобы синхронизировать обе оси, передайте один ресурс `GridLayout` целиком. Например, с ресурсом `FormGrid` из второго примера:

```xml
<views:EventForm GridLayout="{StaticResource FormGrid}" />
```

```xml
<views:CandidatesTable GridLayout="{StaticResource FormGrid}" />
```

Оба представления получают одинаковые шаги, логические размеры и первые видимые индексы. В каждой зоне по-прежнему рисуется только помещающаяся часть сетки.

## 5. Шапка, имена строк и основная таблица

Для трёх связанных поверхностей можно переиспользовать оси `EventColumns` и `CandidateRows` из примеров выше.

```xml
<controls:GridLayout x:Key="HeaderGrid"
                     Columns="{StaticResource EventColumns}">
    <controls:GridLayout.Rows>
        <controls:GridAxis Step="28"
                           Count="1" />
    </controls:GridLayout.Rows>
</controls:GridLayout>

<controls:GridLayout x:Key="BodyGrid"
                     Columns="{StaticResource EventColumns}"
                     Rows="{StaticResource CandidateRows}" />
```

Разместите шапку с `HeaderGrid` над основной таблицей с `BodyGrid`, а панель имён с `NamesGrid` из третьего примера — слева от неё. Прокрутка основной таблицы по горизонтали сдвинет шапку, по вертикали — панель имён. Ветви дерева зон могут различаться: связь проходит через ресурс оси.

## Как влияют размеры зон и сплиттеры

### Как WorkspaceHost строит визуальное дерево

`WorkspaceHost` обходит дерево от `Root`. Каждая `Zone` описывает либо одно представление, либо размещение дочерних зон. Хост применяет следующие правила рекурсивно.

- **Зона с одним представлением** превращается в `Border` с этим представлением в `Child`. Свойства `Style` и `Background` зоны применяются к рамке.
- **Зона с дочерними зонами** создаёт `Grid`. При `Direction="Rows"` каждая дочерняя зона получает строку, при `Direction="Columns"` — колонку. Свойства дочерней зоны `Size`, `MinSize` и `MaxSize` задают размеры этой строки или колонки в родительском `Grid`. Если у составной зоны есть стиль или фон, хост оборачивает её содержимое в `Border`.
- **Между соседними зонами** хост выделяет отдельную строку или колонку размером `Spacing` (по умолчанию 5). Явный `Splitter` помещает туда перетаскиваемый `Thumb`. В обычном промежутке находится декоративный `Border` из шаблона `Gap`.
- **Для двух и более подзон в строках** свойство `SequentialScroll="True"` помещает собранный `Grid` в общий `ScrollViewer`. Поверх него располагается `StackPanel` с кнопкой для каждого `Zone.Title`. Когда зона целиком уходит выше видимой области, появляется её кнопка. Нажатие возвращает к началу зоны с учётом уже закреплённых заголовков.

Например, в нижней части рабочей страницы объявлены форма, разделитель, кандидаты и тестовая таблица. После обработки хостом получается следующее дерево (размеры указаны в логических пикселях WinUI).

```text
Grid (шаблон ScrollingRows)
├─ ScrollViewer
│  └─ Grid
│     ├─ Строка 0 · Auto → Border [ZonePanelStyle] → EventForm
│     ├─ Строка 1 ·   5 → Thumb
│     ├─ Строка 2 · Auto → Border [ZonePanelStyle] → CandidatesTable [CandidatesGrid]
│     ├─ Строка 3 ·   5 → Border [Gap]
│     └─ Строка 4 · Auto → Border [ZonePanelStyle] → CandidatesTable [TestCandidatesGrid]
└─ StackPanel
   ├─ Button «Форма события»
   ├─ Button «Кандидати»
   └─ Button «Тестовая таблица»
```

При размере Auto сетка запрашивает высоту Rows.Count × Rows.Step. Форма определяет число строк по нижней границе полей текущего режима. Перетаскивание разделителя вручную фиксирует размеры соседних зон. Хост дополнительно задаёт ей минимальную высоту по размеру контейнера в пределах ограничений зоны. Благодаря этому последнюю таблицу можно прокрутить до верхнего края. Кнопки стека изначально скрыты и появляются поверх прокручиваемого содержимого. [Шаблоны WinUI](../../Sources/Windows/PersonalEventsBuilder/UDK/WinUI/Layout/Controls/WorkspaceHost.xaml) · [Построение дерева](../../Sources/Windows/PersonalEventsBuilder/UDK/WinUI/Layout/Controls/WorkspaceHost.xaml.cs)

### Влияние на видимые ячейки

Движок рисует только целые ячейки. Начало колонок рассчитывается в координатах окна по общему горизонтальному диапазону участвующих поверхностей. Каждая поверхность показывает только полностью попавшие в неё колонки. Одинаковые номера колонок совпадают по экранной координате X даже при разных левых краях зон. Строки начинаются у верхнего края каждой поверхности.

- **Сплиттер справа от таблицы.** Если форма шире и её границы остаются на месте, сужение таблицы скрывает справа целые колонки только у таблицы.
- **Сплиттер слева от таблицы.** Движение левой границы скрывает или открывает колонки таблицы. Если край проходит внутри колонки 2, первой отображается целая колонка 3 под колонкой 3 формы.
- **Две панели рядом с общей осью колонок.** Каждая показывает колонки общей сетки, целиком попавшие в её горизонтальный диапазон. Колонка, пересечённая сплиттером, может отсутствовать в обеих панелях.
- **Зона уже одной колонки или ниже одной строки.** На этой поверхности не помещается целая ячейка, поэтому сетка не рисуется.

Реализация расчёта доступна в [GridAxis.cs](../../Sources/Windows/PersonalEventsBuilder/UDK/WinUI/Grid/GridAxis.cs), а отрисовка и обработка мыши — в [GridCanvas.xaml.cs](../../Sources/Windows/PersonalEventsBuilder/UDK/WinUI/Grid/GridCanvas.xaml.cs).