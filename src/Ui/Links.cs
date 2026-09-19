namespace Forgeplan.Ui {

/// <summary>
/// Адреса, куда ведут кнопки в окне «Инфо». Единственное место, где они
/// записаны.
///
/// Прибиты в код намеренно. Ссылку, которую можно подменить через конфиг или
/// файл рядом, подменят: мод ставят распаковкой архива, и «перейти на сайт
/// автора» легко превращается в «перейти куда-нибудь ещё». Здесь же её видно
/// в исходниках и нельзя изменить, не пересобрав мод.
///
/// Пустая строка значит «адреса пока нет»: кнопка в окне гаснет и не нажимается.
/// </summary>
public static class Links {
    public const string Site = "https://forgeplanner.pages.dev";

    /// <summary>Профиль автора.</summary>
    public const string Steam = "https://steamcommunity.com/id/Antizip/";

    public const string Github = "https://github.com/Biozip/ForgePlanner";

    public const string Nexus = "https://www.nexusmods.com/profile/biozip";
}

}
