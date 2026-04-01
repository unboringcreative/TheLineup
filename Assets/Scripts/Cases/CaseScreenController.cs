using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Text.RegularExpressions;

[ExecuteAlways]
public class CaseScreenController : MonoBehaviour
{
    private const int SuspectCount = 5;
    private const int EvidenceCount = 3;

    [Header("Data")]
    public CaseLibrarySO caseLibrary;
    [Min(0)] public int caseIndex;

    [Header("Auto Apply")]
    public bool autoApplyInEditor = true;

    [Header("Case Selection")]
    public bool enableCaseSelectionScreen = true;
    public bool bypassCaseSelectionWhileInEditor = true;

    [Header("Selection")]
    public int selectedSuspectIndex = -1;

    private Text caseTitleText;
    private Text suspectsTitleText;
    private Text evidenceTitleText;
    private Text selectedSuspectText;
    private Text detailedInfoTitleText;
    private Text detailedInfoBodyText;
    private Text verdictText;
    private Text explanationText;
    private Button confirmButton;
    private Button closeVerdictButton;
    private Button openCaseSelectionButton;
    private Button openMainMenuButton;
    private GameObject resultPanel;
    private Image resultPanelEvidenceImage;
    private GameObject caseSelectionOverlay;
    private RectTransform caseSelectionList;
    private GameObject interviewPopup;
    private Image interviewPortraitImage;
    private Text interviewNameText;
    private Text interviewDialogueTitleText;
    private Text interviewDialogueBodyText;
    private Button interviewCloseButton;
    private GameObject verdictSelectionOverlay;
    private RectTransform verdictSelectionGrid;
    private Button verdictSelectionConfirmButton;
    private Text verdictSelectionHintText;
    private GameObject mainMenuOverlay;
    private int verdictSelectionIndex = -1;
    private CaseDefinitionSO currentCaseData;
    private Image suspectLineupBackgroundImage;
    private Color verdictDefaultColor;
    private bool verdictColorCached;

    private readonly SuspectSlotView[] suspectViews = new SuspectSlotView[SuspectCount];
    private readonly EvidenceSlotView[] evidenceViews = new EvidenceSlotView[EvidenceCount];
    private readonly List<SuspectProfileSO> currentSuspects = new List<SuspectProfileSO>(SuspectCount);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInPlayMode()
    {
        EnsureComponentOnCanvas();
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void EnsureInEditor()
    {
        UnityEditor.EditorApplication.delayCall += EnsureComponentOnCanvas;
    }
#endif

    private static void EnsureComponentOnCanvas()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
            return;

        if (canvas.GetComponent<CaseScreenController>() == null)
            canvas.gameObject.AddComponent<CaseScreenController>();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            EnsureEvidenceSlotButtons();
            EnsureCaseSelectionOverlay();
            EnsureInterviewPopup();
            EnsureVerdictSelectionOverlay();
            EnsureMainMenuOverlay();
        }

        AutoBind();
        if (Application.isPlaying)
        {
            EnsureOpenMainMenuButton();
            EnsureOpenCaseSelectionButton();
        }

        if (Application.isPlaying)
        {
            if (ShouldOpenCaseSelectionOnStart())
                ShowCaseSelectionOverlay();
            else
                ApplyCurrentCase();
        }
        else if (autoApplyInEditor)
            ApplyCurrentCase();

        ApplyLayoutRules();
    }

    private void OnValidate()
    {
        if (!autoApplyInEditor)
            return;

        AutoBind();
        ApplyCurrentCase();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall -= DelayedEnsureSuspectGridOneRow;
            UnityEditor.EditorApplication.delayCall += DelayedEnsureSuspectGridOneRow;
        }
#endif
    }

    private bool ShouldOpenCaseSelectionOnStart()
    {
        if (!Application.isPlaying || !enableCaseSelectionScreen)
            return false;

        if (Application.isEditor && bypassCaseSelectionWhileInEditor)
            return false;

        return caseLibrary != null && caseLibrary.cases != null && caseLibrary.cases.Count > 0;
    }

    private void EnsureCaseSelectionOverlay()
    {
        if (!Application.isPlaying || caseSelectionOverlay != null)
            return;

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        Font font = GetBuiltinUiFont();

        caseSelectionOverlay = new GameObject("CaseSelectionOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform overlayRect = caseSelectionOverlay.GetComponent<RectTransform>();
        overlayRect.SetParent(canvas.transform, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = caseSelectionOverlay.GetComponent<Image>();
        overlayImage.color = new Color(0.04f, 0.06f, 0.09f, 0.9f);
        overlayImage.raycastTarget = true;

        GameObject panel = new GameObject("CaseSelectionPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.SetParent(caseSelectionOverlay.transform, false);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(920f, 620f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.13f, 0.17f, 0.22f, 0.97f);

        VerticalLayoutGroup panelLayout = panel.GetComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(28, 28, 28, 28);
        panelLayout.spacing = 16;
        panelLayout.childControlHeight = true;
        panelLayout.childControlWidth = true;
        panelLayout.childForceExpandHeight = false;
        panelLayout.childForceExpandWidth = true;

        ContentSizeFitter panelFitter = panel.GetComponent<ContentSizeFitter>();
        panelFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        panelFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        CreateCaseSelectionText(panel.transform, font, "Select A Case", 30, TextAnchor.MiddleCenter, FontStyle.Bold, 44f);

        string subtitle = "Choose a case to open and start investigating.";
        CreateCaseSelectionText(panel.transform, font, subtitle, 15, TextAnchor.MiddleCenter, FontStyle.Normal, 52f);

        GameObject scrollView = new GameObject("CaseSelectionScroll", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask), typeof(ScrollRect));
        RectTransform scrollRectTransform = scrollView.GetComponent<RectTransform>();
        scrollRectTransform.SetParent(panel.transform, false);
        LayoutElement scrollLayout = scrollView.AddComponent<LayoutElement>();
        scrollLayout.preferredHeight = 430f;
        Image scrollImage = scrollView.GetComponent<Image>();
        scrollImage.color = new Color(0.09f, 0.11f, 0.15f, 0.9f);
        scrollView.GetComponent<Mask>().showMaskGraphic = true;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.SetParent(scrollView.transform, false);
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        caseSelectionList = content.GetComponent<RectTransform>();
        caseSelectionList.SetParent(viewport.transform, false);
        caseSelectionList.anchorMin = new Vector2(0f, 1f);
        caseSelectionList.anchorMax = new Vector2(1f, 1f);
        caseSelectionList.pivot = new Vector2(0.5f, 1f);
        caseSelectionList.offsetMin = new Vector2(12f, 0f);
        caseSelectionList.offsetMax = new Vector2(-12f, 0f);

        VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(0, 0, 0, 0);
        contentLayout.spacing = 10;
        contentLayout.childControlHeight = true;
        contentLayout.childControlWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childForceExpandWidth = true;

        ContentSizeFitter contentFitter = content.GetComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        ScrollRect scrollRect = scrollView.GetComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = caseSelectionList;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        GameObject footer = new GameObject("Footer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        RectTransform footerRect = footer.GetComponent<RectTransform>();
        footerRect.SetParent(panel.transform, false);
        HorizontalLayoutGroup footerLayout = footer.GetComponent<HorizontalLayoutGroup>();
        footerLayout.spacing = 12;
        footerLayout.childAlignment = TextAnchor.MiddleCenter;
        footerLayout.childControlHeight = true;
        footerLayout.childControlWidth = false;
        footerLayout.childForceExpandHeight = false;
        footerLayout.childForceExpandWidth = false;

        CreateFooterButton(footer.transform, font, "Open Case", () => HideCaseSelectionOverlay(), 180f);

        RebuildCaseSelectionOverlay();
        caseSelectionOverlay.SetActive(false);
    }

    private void EnsureOpenCaseSelectionButton()
    {
        if (!Application.isPlaying || openCaseSelectionButton != null)
            return;

        Transform host = FindDeep(transform, "TopBar");
        if (host == null)
            return;

        Transform existing = FindDeepLocal(host, "OpenCaseSelectionButton");
        if (existing != null)
        {
            openCaseSelectionButton = existing.GetComponent<Button>();
            if (openCaseSelectionButton != null)
            {
                RectTransform existingRect = openCaseSelectionButton.transform as RectTransform;
                if (existingRect != null)
                    existingRect.anchoredPosition = new Vector2(-188f, 0f);

                openCaseSelectionButton.onClick.RemoveAllListeners();
                openCaseSelectionButton.onClick.AddListener(ShowCaseSelectionOverlay);
            }
            return;
        }

        Font font = GetBuiltinUiFont();
        GameObject go = new GameObject("OpenCaseSelectionButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(host, false);

        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(164f, 34f);
        rt.anchoredPosition = new Vector2(-188f, 0f);

        Image bg = go.GetComponent<Image>();
        bg.color = new Color(0.25f, 0.39f, 0.65f, 1f);

        openCaseSelectionButton = go.GetComponent<Button>();
        openCaseSelectionButton.onClick.AddListener(ShowCaseSelectionOverlay);

        GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.SetParent(go.transform, false);
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;

        Text label = labelGO.GetComponent<Text>();
        label.font = font;
        label.fontSize = 15;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.text = "Case Select";
    }

    private void EnsureOpenMainMenuButton()
    {
        if (!Application.isPlaying || openMainMenuButton != null)
            return;

        Transform host = FindDeep(transform, "TopBar");
        if (host == null)
            return;

        Transform existing = FindDeepLocal(host, "OpenMainMenuButton");
        if (existing != null)
        {
            openMainMenuButton = existing.GetComponent<Button>();
            if (openMainMenuButton != null)
            {
                RectTransform existingRect = openMainMenuButton.transform as RectTransform;
                if (existingRect != null)
                    existingRect.anchoredPosition = new Vector2(-16f, 0f);

                openMainMenuButton.onClick.RemoveAllListeners();
                openMainMenuButton.onClick.AddListener(ToggleMainMenuOverlay);
            }
            return;
        }

        Font font = GetBuiltinUiFont();
        GameObject go = new GameObject("OpenMainMenuButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(host, false);

        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(116f, 34f);
        rt.anchoredPosition = new Vector2(-16f, 0f);

        Image bg = go.GetComponent<Image>();
        bg.color = new Color(0.19f, 0.24f, 0.32f, 1f);

        openMainMenuButton = go.GetComponent<Button>();
        openMainMenuButton.onClick.AddListener(ToggleMainMenuOverlay);

        GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.SetParent(go.transform, false);
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;

        Text label = labelGO.GetComponent<Text>();
        label.font = font;
        label.fontSize = 15;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.text = "Menu";
    }

    private void EnsureMainMenuOverlay()
    {
        if (!Application.isPlaying || mainMenuOverlay != null)
            return;

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        Font font = GetBuiltinUiFont();

        mainMenuOverlay = new GameObject("MainMenuOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform overlayRect = mainMenuOverlay.GetComponent<RectTransform>();
        overlayRect.SetParent(canvas.transform, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = mainMenuOverlay.GetComponent<Image>();
        overlayImage.color = new Color(0.03f, 0.05f, 0.08f, 0.94f);
        overlayImage.raycastTarget = true;

        GameObject panel = new GameObject("MainMenuPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.SetParent(mainMenuOverlay.transform, false);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(520f, 260f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.12f, 0.16f, 0.22f, 0.98f);

        VerticalLayoutGroup panelLayout = panel.GetComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(28, 28, 28, 28);
        panelLayout.spacing = 18;
        panelLayout.childAlignment = TextAnchor.MiddleCenter;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        CreateCaseSelectionText(panel.transform, font, "Menu", 32, TextAnchor.MiddleCenter, FontStyle.Bold, 54f);
        CreateCaseSelectionText(panel.transform, font, "Open while in-game. More menu options can be added here.", 16, TextAnchor.MiddleCenter, FontStyle.Normal, 56f);
        CreateFooterButton(panel.transform, font, "Quit", HandleQuitRequested, 220f);

        mainMenuOverlay.SetActive(false);
    }

    private void ToggleMainMenuOverlay()
    {
        EnsureMainMenuOverlay();
        if (mainMenuOverlay == null)
            return;

        bool shouldShow = !mainMenuOverlay.activeSelf;
        mainMenuOverlay.SetActive(shouldShow);

        if (shouldShow)
        {
            if (resultPanel != null)
                resultPanel.SetActive(false);
            if (caseSelectionOverlay != null)
                caseSelectionOverlay.SetActive(false);
        }
    }

    private static void HandleQuitRequested()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void RebuildCaseSelectionOverlay()
    {
        if (caseSelectionList == null)
            return;

        for (int i = caseSelectionList.childCount - 1; i >= 0; i--)
            Destroy(caseSelectionList.GetChild(i).gameObject);

        Font font = GetBuiltinUiFont();
        if (caseLibrary == null || caseLibrary.cases == null || caseLibrary.cases.Count == 0)
        {
            CreateCaseSelectionText(caseSelectionList, font, "No cases available. Import a case first.", 18, TextAnchor.MiddleCenter, FontStyle.Normal, 60f);
            return;
        }

        for (int i = 0; i < caseLibrary.cases.Count; i++)
        {
            CaseDefinitionSO caseData = caseLibrary.cases[i];
            if (caseData == null)
                continue;

            GameObject buttonObject = new GameObject($"CaseButton_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(caseSelectionList, false);

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = new Color(0.21f, 0.26f, 0.33f, 1f);

            Button button = buttonObject.GetComponent<Button>();
            int capturedIndex = i;
            button.onClick.AddListener(() => SelectCaseFromOverlay(capturedIndex));

            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 112f;

            HorizontalLayoutGroup rowLayout = buttonObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(16, 16, 12, 12);
            rowLayout.spacing = 14;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childForceExpandWidth = false;

            GameObject featuredGo = new GameObject("FeaturedImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            RectTransform featuredRect = featuredGo.GetComponent<RectTransform>();
            featuredRect.SetParent(buttonObject.transform, false);

            Image featuredImage = featuredGo.GetComponent<Image>();
            featuredImage.sprite = caseData.featuredImage;
            featuredImage.preserveAspect = true;
            featuredImage.color = caseData.featuredImage != null ? Color.white : new Color(1f, 1f, 1f, 0.08f);

            LayoutElement featuredLayout = featuredGo.GetComponent<LayoutElement>();
            featuredLayout.minWidth = 140f;
            featuredLayout.preferredWidth = 140f;
            featuredLayout.minHeight = 84f;
            featuredLayout.preferredHeight = 84f;
            featuredLayout.flexibleWidth = 0f;

            GameObject textColumn = new GameObject("TextColumn", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            RectTransform textColumnRect = textColumn.GetComponent<RectTransform>();
            textColumnRect.SetParent(buttonObject.transform, false);

            VerticalLayoutGroup textLayout = textColumn.GetComponent<VerticalLayoutGroup>();
            textLayout.spacing = 4f;
            textLayout.childAlignment = TextAnchor.MiddleLeft;
            textLayout.childControlHeight = true;
            textLayout.childControlWidth = true;
            textLayout.childForceExpandHeight = false;
            textLayout.childForceExpandWidth = true;

            LayoutElement textColumnLayout = textColumn.GetComponent<LayoutElement>();
            textColumnLayout.flexibleWidth = 1f;
            textColumnLayout.minWidth = 320f;

            GameObject titleGo = new GameObject("CaseTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
            RectTransform titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.SetParent(textColumn.transform, false);

            Text titleText = titleGo.GetComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 30;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            titleText.verticalOverflow = VerticalWrapMode.Truncate;
            titleText.color = new Color(0.95f, 0.96f, 0.98f, 1f);
            titleText.text = caseData.caseTitle;

            LayoutElement titleLayout = titleGo.GetComponent<LayoutElement>();
            titleLayout.preferredHeight = 44f;

            string address = caseData.locationAddressOrBusiness;
            string city = caseData.locationCity;
            string country = caseData.locationCountry;

            string region = string.IsNullOrWhiteSpace(city)
                ? country
                : (string.IsNullOrWhiteSpace(country) ? city : city + ", " + country);

            string locationLine = string.IsNullOrWhiteSpace(address)
                ? region
                : (string.IsNullOrWhiteSpace(region) ? address : address + " - " + region);

            if (string.IsNullOrWhiteSpace(locationLine))
                locationLine = "Location unknown";

            GameObject locationGo = new GameObject("CaseLocation", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
            RectTransform locationRect = locationGo.GetComponent<RectTransform>();
            locationRect.SetParent(textColumn.transform, false);

            Text locationText = locationGo.GetComponent<Text>();
            locationText.font = font;
            locationText.fontSize = 16;
            locationText.fontStyle = FontStyle.Normal;
            locationText.alignment = TextAnchor.MiddleLeft;
            locationText.horizontalOverflow = HorizontalWrapMode.Wrap;
            locationText.verticalOverflow = VerticalWrapMode.Truncate;
            locationText.color = new Color(0.86f, 0.9f, 0.96f, 0.96f);
            locationText.text = locationLine;

            LayoutElement locationLayout = locationGo.GetComponent<LayoutElement>();
            locationLayout.preferredHeight = 28f;
        }
    }

    private void ShowCaseSelectionOverlay()
    {
        EnsureCaseSelectionOverlay();
        RebuildCaseSelectionOverlay();
        if (caseSelectionOverlay != null)
            caseSelectionOverlay.SetActive(true);

        if (resultPanel != null)
            resultPanel.SetActive(false);
    }

    private void HideCaseSelectionOverlay()
    {
        if (caseSelectionOverlay != null)
            caseSelectionOverlay.SetActive(false);

        ApplyCurrentCase();
    }

    private void SelectCaseFromOverlay(int index)
    {
        caseIndex = index;
        selectedSuspectIndex = -1;
        HideCaseSelectionOverlay();
    }

    private static Text CreateCaseSelectionText(Transform parent, Font font, string value, int fontSize, TextAnchor alignment, FontStyle style, float preferredHeight)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        Text text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.color = new Color(0.95f, 0.96f, 0.98f, 1f);
        text.text = value;

        LayoutElement le = go.GetComponent<LayoutElement>();
        le.preferredHeight = preferredHeight;
        return text;
    }

    private static Font GetBuiltinUiFont()
    {
        Font helvetica = Resources.Load<Font>("Fonts/HelveticaNeueLTCom-Roman");
        if (helvetica != null)
            return helvetica;

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static void CreateFooterButton(Transform parent, Font font, string label, UnityEngine.Events.UnityAction onClick, float width)
    {
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        Image bg = go.GetComponent<Image>();
        bg.color = new Color(0.26f, 0.39f, 0.65f, 1f);

        Button button = go.GetComponent<Button>();
        button.onClick.AddListener(onClick);

        LayoutElement le = go.GetComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = 44f;

        GameObject textGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.SetParent(go.transform, false);
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        Text text = textGO.GetComponent<Text>();
        text.font = font;
        text.fontSize = 16;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = label;
    }

    private void EnsureInterviewPopup()
    {
        if (!Application.isPlaying || interviewPopup != null)
            return;

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        Font font = GetBuiltinUiFont();

        interviewPopup = new GameObject("InterviewPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform overlayRect = interviewPopup.GetComponent<RectTransform>();
        overlayRect.SetParent(canvas.transform, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = interviewPopup.GetComponent<Image>();
        overlayImage.color = new Color(0.03f, 0.05f, 0.08f, 0.92f);
        overlayImage.raycastTarget = true;

        GameObject panel = new GameObject("InterviewPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.SetParent(interviewPopup.transform, false);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(980f, 640f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.12f, 0.16f, 0.22f, 0.98f);

        GameObject left = new GameObject("PortraitViewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
        RectTransform leftRect = left.GetComponent<RectTransform>();
        leftRect.SetParent(panel.transform, false);
        leftRect.anchorMin = new Vector2(0f, 0f);
        leftRect.anchorMax = new Vector2(0.44f, 1f);
        leftRect.offsetMin = new Vector2(16f, 16f);
        leftRect.offsetMax = new Vector2(-8f, -16f);
        Image leftBg = left.GetComponent<Image>();
        leftBg.color = new Color(0.08f, 0.1f, 0.14f, 1f);
        left.GetComponent<Mask>().showMaskGraphic = true;

        GameObject portraitGo = new GameObject("PortraitImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform portraitRect = portraitGo.GetComponent<RectTransform>();
        portraitRect.SetParent(left.transform, false);
        portraitRect.anchorMin = new Vector2(0.5f, 1f);
        portraitRect.anchorMax = new Vector2(0.5f, 1f);
        portraitRect.pivot = new Vector2(0.5f, 1f);
        portraitRect.sizeDelta = new Vector2(760f, 1220f);
        portraitRect.anchoredPosition = new Vector2(0f, -8f);

        interviewPortraitImage = portraitGo.GetComponent<Image>();
        interviewPortraitImage.preserveAspect = true;
        interviewPortraitImage.color = new Color(1f, 1f, 1f, 0f);

        GameObject right = new GameObject("DialoguePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rightRect = right.GetComponent<RectTransform>();
        rightRect.SetParent(panel.transform, false);
        rightRect.anchorMin = new Vector2(0.44f, 0f);
        rightRect.anchorMax = new Vector2(1f, 1f);
        rightRect.offsetMin = new Vector2(8f, 16f);
        rightRect.offsetMax = new Vector2(-16f, -16f);
        Image rightBg = right.GetComponent<Image>();
        rightBg.color = new Color(0.1f, 0.13f, 0.18f, 1f);

        interviewNameText = CreateCaseSelectionText(right.transform, font, "Suspect", 30, TextAnchor.UpperLeft, FontStyle.Bold, 44f);
        RectTransform nameRect = interviewNameText.transform as RectTransform;
        if (nameRect != null)
        {
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.offsetMin = new Vector2(18f, -56f);
            nameRect.offsetMax = new Vector2(-18f, -10f);
        }

        interviewDialogueTitleText = CreateCaseSelectionText(right.transform, font, string.Empty, 20, TextAnchor.UpperLeft, FontStyle.Bold, 34f);
        RectTransform titleRect = interviewDialogueTitleText.transform as RectTransform;
        if (titleRect != null)
        {
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(18f, -98f);
            titleRect.offsetMax = new Vector2(-18f, -66f);
        }

        interviewDialogueBodyText = CreateCaseSelectionText(right.transform, font, "...", 20, TextAnchor.UpperLeft, FontStyle.Normal, 420f);
        RectTransform bodyRect = interviewDialogueBodyText.transform as RectTransform;
        if (bodyRect != null)
        {
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.offsetMin = new Vector2(18f, 80f);
            bodyRect.offsetMax = new Vector2(-18f, -110f);
        }

        if (interviewDialogueBodyText != null)
            interviewDialogueBodyText.fontStyle = FontStyle.Normal;

        GameObject closeGo = new GameObject("CloseInterviewButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform closeRect = closeGo.GetComponent<RectTransform>();
        closeRect.SetParent(right.transform, false);
        closeRect.anchorMin = new Vector2(0.5f, 0f);
        closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0.5f);
        closeRect.sizeDelta = new Vector2(170f, 44f);
        closeRect.anchoredPosition = new Vector2(0f, 34f);

        Image closeBg = closeGo.GetComponent<Image>();
        closeBg.color = new Color(0.24f, 0.37f, 0.62f, 1f);

        interviewCloseButton = closeGo.GetComponent<Button>();
        interviewCloseButton.onClick.AddListener(CloseInterviewPopup);

        GameObject closeLabelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform closeLabelRect = closeLabelGo.GetComponent<RectTransform>();
        closeLabelRect.SetParent(closeGo.transform, false);
        closeLabelRect.anchorMin = Vector2.zero;
        closeLabelRect.anchorMax = Vector2.one;
        closeLabelRect.offsetMin = Vector2.zero;
        closeLabelRect.offsetMax = Vector2.zero;

        Text closeLabel = closeLabelGo.GetComponent<Text>();
        closeLabel.font = font;
        closeLabel.fontSize = 18;
        closeLabel.fontStyle = FontStyle.Bold;
        closeLabel.alignment = TextAnchor.MiddleCenter;
        closeLabel.color = Color.white;
        closeLabel.text = "Close Interview";

        interviewPopup.SetActive(false);
    }

    private void OpenInterviewPopup(int index)
    {
        EnsureInterviewPopup();
        if (interviewPopup == null)
            return;

        SuspectProfileSO suspect = (index >= 0 && index < currentSuspects.Count) ? currentSuspects[index] : null;

        if (interviewNameText != null)
            interviewNameText.text = suspect != null && !string.IsNullOrWhiteSpace(suspect.displayName)
                ? suspect.displayName
                : $"Suspect {index + 1}";

        if (interviewDialogueTitleText != null)
            interviewDialogueTitleText.text = string.Empty;

        if (interviewDialogueBodyText != null)
            interviewDialogueBodyText.text = BuildSuspectDialoguePlaceholder(suspect);

        if (interviewPortraitImage != null)
        {
            interviewPortraitImage.sprite = suspect != null ? suspect.portrait : null;
            interviewPortraitImage.color = interviewPortraitImage.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }

        interviewPopup.SetActive(true);
    }

    private void CloseInterviewPopup()
    {
        if (interviewPopup != null)
            interviewPopup.SetActive(false);

        selectedSuspectIndex = -1;
        UpdateSelectionUI();
    }

    private void EnsureVerdictSelectionOverlay()
    {
        if (!Application.isPlaying || verdictSelectionOverlay != null)
            return;

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        Font font = GetBuiltinUiFont();

        verdictSelectionOverlay = new GameObject("VerdictSelectionOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform overlayRect = verdictSelectionOverlay.GetComponent<RectTransform>();
        overlayRect.SetParent(canvas.transform, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = verdictSelectionOverlay.GetComponent<Image>();
        overlayImage.color = new Color(0.03f, 0.05f, 0.08f, 0.92f);
        overlayImage.raycastTarget = true;

        GameObject panel = new GameObject("VerdictSelectionPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.SetParent(verdictSelectionOverlay.transform, false);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(980f, 600f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.12f, 0.16f, 0.22f, 0.98f);

        GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.SetParent(panel.transform, false);
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(20f, -52f);
        titleRect.offsetMax = new Vector2(-20f, -8f);
        Text titleText = titleGo.GetComponent<Text>();
        titleText.font = font;
        titleText.fontSize = 30;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.UpperCenter;
        titleText.color = new Color(0.95f, 0.96f, 0.98f, 1f);
        titleText.text = "Make A Verdict";

        GameObject hintGo = new GameObject("Hint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform hintRect = hintGo.GetComponent<RectTransform>();
        hintRect.SetParent(panel.transform, false);
        hintRect.anchorMin = new Vector2(0f, 1f);
        hintRect.anchorMax = new Vector2(1f, 1f);
        hintRect.offsetMin = new Vector2(20f, -90f);
        hintRect.offsetMax = new Vector2(-20f, -50f);
        verdictSelectionHintText = hintGo.GetComponent<Text>();
        verdictSelectionHintText.font = font;
        verdictSelectionHintText.fontSize = 16;
        verdictSelectionHintText.fontStyle = FontStyle.Normal;
        verdictSelectionHintText.alignment = TextAnchor.UpperCenter;
        verdictSelectionHintText.color = new Color(0.84f, 0.88f, 0.95f, 1f);
        verdictSelectionHintText.text = "Pick one suspect to accuse.";

        GameObject gridGo = new GameObject("VerdictGrid", typeof(RectTransform), typeof(GridLayoutGroup));
        verdictSelectionGrid = gridGo.GetComponent<RectTransform>();
        verdictSelectionGrid.SetParent(panel.transform, false);
        verdictSelectionGrid.anchorMin = new Vector2(0f, 0f);
        verdictSelectionGrid.anchorMax = new Vector2(1f, 1f);
        verdictSelectionGrid.offsetMin = new Vector2(26f, 86f);
        verdictSelectionGrid.offsetMax = new Vector2(-26f, -110f);

        GridLayoutGroup grid = gridGo.GetComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = SuspectCount;
        grid.cellSize = new Vector2(176f, 300f);
        grid.spacing = new Vector2(12f, 12f);
        grid.childAlignment = TextAnchor.UpperCenter;

        CreateVerdictFooterButton(panel.transform, font, "Close", CloseVerdictSelectionOverlay, new Vector2(360f, 44f), new Vector2(-190f, 30f));
        verdictSelectionConfirmButton = CreateVerdictFooterButton(panel.transform, font, "Accuse Selected", ConfirmVerdictSelection, new Vector2(360f, 44f), new Vector2(190f, 30f));
        verdictSelectionConfirmButton.interactable = false;

        verdictSelectionOverlay.SetActive(false);
    }

    private Button CreateVerdictFooterButton(Transform parent, Font font, string label, UnityEngine.Events.UnityAction onClick, Vector2 size, Vector2 anchoredPosition)
    {
        GameObject go = new GameObject(label + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPosition;

        Image bg = go.GetComponent<Image>();
        bg.color = new Color(0.24f, 0.37f, 0.62f, 1f);

        Button button = go.GetComponent<Button>();
        button.onClick.AddListener(onClick);

        GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.SetParent(go.transform, false);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        Text t = textGo.GetComponent<Text>();
        t.font = font;
        t.fontSize = 18;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = label;
        return button;
    }

    private void OpenVerdictSelectionOverlay()
    {
        if (currentCaseData == null)
            return;

        CloseInterviewPopup();

        EnsureVerdictSelectionOverlay();
        if (verdictSelectionOverlay == null)
            return;

        verdictSelectionIndex = -1;
        RebuildVerdictSelectionGrid();
        verdictSelectionOverlay.SetActive(true);
    }

    private void RebuildVerdictSelectionGrid()
    {
        if (verdictSelectionGrid == null)
            return;

        for (int i = verdictSelectionGrid.childCount - 1; i >= 0; i--)
            Destroy(verdictSelectionGrid.GetChild(i).gameObject);

        for (int i = 0; i < SuspectCount; i++)
        {
            SuspectProfileSO suspect = i < currentSuspects.Count ? currentSuspects[i] : null;
            string displayName = suspect != null && !string.IsNullOrWhiteSpace(suspect.displayName) ? suspect.displayName : $"Suspect {i + 1}";
            CreateVerdictCandidateButton(i, displayName, suspect != null ? suspect.portrait : null);
        }

        RefreshVerdictSelectionState();
    }

    private void CreateVerdictCandidateButton(int index, string displayName, Sprite portrait)
    {
        Font font = GetBuiltinUiFont();
        GameObject buttonGo = new GameObject($"VerdictCandidate_{index}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.SetParent(verdictSelectionGrid, false);

        Image bg = buttonGo.GetComponent<Image>();
        bg.color = new Color(0.16f, 0.20f, 0.27f, 1f);

        Button button = buttonGo.GetComponent<Button>();
        button.onClick.AddListener(() =>
        {
            verdictSelectionIndex = index;
            RefreshVerdictSelectionState();
        });

        GameObject viewportGo = new GameObject("HeadshotViewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
        RectTransform viewportRect = viewportGo.GetComponent<RectTransform>();
        viewportRect.SetParent(buttonGo.transform, false);
        viewportRect.anchorMin = new Vector2(0.5f, 1f);
        viewportRect.anchorMax = new Vector2(0.5f, 1f);
        viewportRect.pivot = new Vector2(0.5f, 1f);
        viewportRect.sizeDelta = new Vector2(140f, 140f);
        viewportRect.anchoredPosition = new Vector2(0f, -12f);
        Image viewportBg = viewportGo.GetComponent<Image>();
        viewportBg.color = new Color(0.08f, 0.10f, 0.14f, 1f);
        viewportGo.GetComponent<Mask>().showMaskGraphic = true;

        GameObject portraitGo = new GameObject("Headshot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform portraitRect = portraitGo.GetComponent<RectTransform>();
        portraitRect.SetParent(viewportGo.transform, false);
        portraitRect.anchorMin = new Vector2(0.5f, 1f);
        portraitRect.anchorMax = new Vector2(0.5f, 1f);
        portraitRect.pivot = new Vector2(0.5f, 1f);
        portraitRect.sizeDelta = new Vector2(240f, 360f);
        portraitRect.anchoredPosition = new Vector2(0f, -4f);
        Image portraitImage = portraitGo.GetComponent<Image>();
        portraitImage.sprite = portrait;
        portraitImage.preserveAspect = true;
        portraitImage.color = portrait != null ? Color.white : new Color(1f, 1f, 1f, 0.1f);

        GameObject nameGo = new GameObject("Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform nameRect = nameGo.GetComponent<RectTransform>();
        nameRect.SetParent(buttonGo.transform, false);
        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(1f, 0f);
        nameRect.pivot = new Vector2(0.5f, 0f);
        nameRect.offsetMin = new Vector2(10f, 10f);
        nameRect.offsetMax = new Vector2(-10f, 72f);

        GameObject nameBackgroundGo = new GameObject("NameBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform nameBackgroundRect = nameBackgroundGo.GetComponent<RectTransform>();
        nameBackgroundRect.SetParent(buttonGo.transform, false);
        nameBackgroundRect.anchorMin = new Vector2(0f, 0f);
        nameBackgroundRect.anchorMax = new Vector2(1f, 0f);
        nameBackgroundRect.pivot = new Vector2(0.5f, 0f);
        nameBackgroundRect.offsetMin = new Vector2(6f, 6f);
        nameBackgroundRect.offsetMax = new Vector2(-6f, 76f);
        Image nameBackground = nameBackgroundGo.GetComponent<Image>();
        nameBackground.color = new Color(0.04f, 0.06f, 0.1f, 0.86f);
        nameBackground.raycastTarget = false;

        nameBackgroundGo.transform.SetSiblingIndex(nameGo.transform.GetSiblingIndex());
        nameGo.transform.SetSiblingIndex(nameBackgroundGo.transform.GetSiblingIndex() + 1);

        Text nameText = nameGo.GetComponent<Text>();
        nameText.font = font;
        nameText.fontSize = 17;
        nameText.fontStyle = FontStyle.Bold;
        nameText.alignment = TextAnchor.MiddleCenter;
        nameText.color = new Color(0.96f, 0.97f, 0.99f, 1f);
        nameText.text = displayName;
    }

    private void RefreshVerdictSelectionState()
    {
        if (verdictSelectionGrid != null)
        {
            for (int i = 0; i < verdictSelectionGrid.childCount; i++)
            {
                Image bg = verdictSelectionGrid.GetChild(i).GetComponent<Image>();
                if (bg == null)
                    continue;

                bool selected = i == verdictSelectionIndex;
                bg.color = selected
                    ? new Color(0.93f, 0.76f, 0.24f, 1f)
                    : new Color(0.16f, 0.20f, 0.27f, 1f);
            }
        }

        if (verdictSelectionConfirmButton != null)
            verdictSelectionConfirmButton.interactable = verdictSelectionIndex >= 0;

        if (verdictSelectionHintText != null)
        {
            verdictSelectionHintText.text = verdictSelectionIndex >= 0
                ? "Selected: " + GetSuspectName(verdictSelectionIndex)
                : "Pick one suspect to accuse.";
        }
    }

    private void CloseVerdictSelectionOverlay()
    {
        if (verdictSelectionOverlay != null)
            verdictSelectionOverlay.SetActive(false);

        verdictSelectionIndex = -1;
    }

    private void ConfirmVerdictSelection()
    {
        if (currentCaseData == null || verdictSelectionIndex < 0 || verdictSelectionIndex >= SuspectCount)
            return;

        bool correct = verdictSelectionIndex == currentCaseData.guiltySuspectIndex;
        string guiltyName = GetSuspectName(currentCaseData.guiltySuspectIndex);

        if (verdictText != null)
        {
            verdictText.text = correct ? "Correct Accusation" : "Wrong Accusation";
            verdictText.color = correct ? new Color(0.32f, 0.87f, 0.5f, 1f) : new Color(0.95f, 0.35f, 0.35f, 1f);
        }

        if (explanationText != null)
        {
            if (correct)
                explanationText.text = currentCaseData.explanation;
            else
                explanationText.text = "Your accusation was incorrect. The guilty suspect was " + guiltyName + ".\n\n" + currentCaseData.explanation;
        }

        if (resultPanel != null)
            resultPanel.SetActive(true);

        if (resultPanelEvidenceImage != null)
            resultPanelEvidenceImage.gameObject.SetActive(false);

        CloseVerdictSelectionOverlay();
    }

    public void ApplyCurrentCase()
    {
        CloseInterviewPopup();
        CloseVerdictSelectionOverlay();

        if (caseLibrary == null || caseLibrary.cases == null || caseLibrary.cases.Count == 0)
        {
            currentCaseData = null;
            ApplyPlaceholders();
            return;
        }

        int index = Mathf.Clamp(caseIndex, 0, caseLibrary.cases.Count - 1);
        CaseDefinitionSO caseData = caseLibrary.cases[index];

        if (caseData == null)
        {
            currentCaseData = null;
            ApplyPlaceholders();
            return;
        }

        currentCaseData = caseData;
        ApplySuspectLineupBackground(caseData.locationImage != null ? caseData.locationImage : caseData.featuredImage);

        SetText(caseTitleText, caseData.caseTitle);
        SetText(verdictText, caseData.verdictTitle);
        SetText(explanationText, caseData.explanation);

        if (resultPanel != null)
            resultPanel.SetActive(false);

        if (verdictText != null && verdictColorCached)
            verdictText.color = verdictDefaultColor;

        List<SuspectProfileSO> orderedSuspects = BuildOrderedSuspectList(caseData);
        currentSuspects.Clear();
        currentSuspects.AddRange(orderedSuspects);

        for (int i = 0; i < suspectViews.Length; i++)
        {
            SuspectProfileSO suspect = i < orderedSuspects.Count ? orderedSuspects[i] : null;
            suspectViews[i].Apply(suspect, i + 1);
        }

        for (int i = 0; i < evidenceViews.Length; i++)
        {
            EvidenceProfileSO evidence = i < caseData.evidence.Length ? caseData.evidence[i] : null;
            evidenceViews[i].Apply(evidence, i + 1);
        }

        ForceApplyEvidenceToBottomPanel(caseData);

        UpdateSelectionUI();
    }

    private void ForceApplyEvidenceToBottomPanel(CaseDefinitionSO caseData)
    {
        Transform grid = FindPreferredEvidenceGrid();
        if (grid == null)
            return;

        for (int i = 0; i < EvidenceCount; i++)
        {
            EvidenceProfileSO evidence = (caseData != null && caseData.evidence != null && i < caseData.evidence.Length)
                ? caseData.evidence[i]
                : null;

            Transform slot = FindDirectChild(grid, $"EvidenceSlot_{i + 1:00}");
            if (slot == null)
                continue;

            Image image = FindDeepLocal(slot, "EvidenceImage")?.GetComponent<Image>();
            Text label = FindDeepLocal(slot, "EvidenceLabel")?.GetComponent<Text>();
            Text desc = FindDeepLocal(slot, "EvidenceDescription")?.GetComponent<Text>();

            if (image != null)
            {
                image.sprite = evidence != null ? evidence.image : null;
                image.color = (evidence != null && evidence.image != null)
                    ? Color.white
                    : new Color(0.45f, 0.55f, 0.65f, 1f);
                image.preserveAspect = true;
            }

            if (label != null)
                label.text = evidence != null ? evidence.title : $"Evidence {i + 1}";

            if (desc != null)
                desc.text = evidence != null ? BuildEvidenceDescription(evidence) : "Add evidence profile.";

            ApplyEvidenceSlotLayout(slot, label, desc, image);
        }
    }

    private static void ApplyEvidenceSlotLayout(Transform slotRoot, Text label, Text description, Image image)
    {
        if (slotRoot == null)
            return;

        Image slotBackground = slotRoot.GetComponent<Image>();
        if (slotBackground != null)
            slotBackground.color = new Color(0.09f, 0.11f, 0.13f, 0.82f);

        if (description != null)
            description.gameObject.SetActive(false);

        HorizontalLayoutGroup rowLayout = slotRoot.GetComponent<HorizontalLayoutGroup>();
        if (rowLayout != null)
        {
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.spacing = 14f;
            rowLayout.padding = new RectOffset(14, 14, 10, 10);
        }

        RectTransform labelRect = label != null ? label.transform as RectTransform : null;
        if (labelRect != null)
        {
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            labelRect.SetSiblingIndex(1);
        }

        if (label != null)
        {
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleLeft;
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(0.94f, 0.92f, 0.86f, 1f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            LayoutElement labelLayout = label.GetComponent<LayoutElement>();
            if (labelLayout == null)
                labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.ignoreLayout = false;
            labelLayout.minWidth = 120f;
            labelLayout.preferredWidth = 0f;
            labelLayout.minHeight = 0f;
            labelLayout.preferredHeight = -1f;
            labelLayout.flexibleWidth = 1f;
            labelLayout.flexibleHeight = 0f;
        }

        RectTransform imageRect = image != null ? image.transform as RectTransform : null;
        if (imageRect != null)
        {
            float editorScale = Mathf.Max(1f, Mathf.Max(imageRect.localScale.x, imageRect.localScale.y));
            float thumbSize = 104f * editorScale;

            imageRect.anchorMin = new Vector2(0.5f, 0.5f);
            imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            imageRect.pivot = new Vector2(0.5f, 0.5f);
            imageRect.sizeDelta = new Vector2(thumbSize, thumbSize);
            imageRect.anchoredPosition = Vector2.zero;
            imageRect.localScale = Vector3.one;
            imageRect.SetSiblingIndex(0);

            LayoutElement imageLayout = image.GetComponent<LayoutElement>();
            if (imageLayout == null)
                imageLayout = image.gameObject.AddComponent<LayoutElement>();
            imageLayout.ignoreLayout = false;
            imageLayout.minWidth = thumbSize;
            imageLayout.preferredWidth = thumbSize;
            imageLayout.minHeight = thumbSize;
            imageLayout.preferredHeight = thumbSize;
            imageLayout.flexibleWidth = 0f;
            imageLayout.flexibleHeight = 0f;
        }
    }

    private static string BuildEvidenceDescription(EvidenceProfileSO evidence)
    {
        if (evidence == null)
            return "No evidence details available.";

        string description = string.IsNullOrWhiteSpace(evidence.description) ? "No evidence details." : evidence.description;
        string location = string.IsNullOrWhiteSpace(evidence.discoveryLocation) ? "Unknown" : evidence.discoveryLocation;
        return description + "\n\nDiscovery Location: " + location;
    }

    private static float ComputeHeightScale(string heightText)
    {
        if (!TryParseHeightInches(heightText, out float inches))
            return 1f;

        const float baselineInches = 69f; // 5'9"
        const float perInchScale = 0.0125f; // 1.25% per inch
        float scale = 1f + ((inches - baselineInches) * perInchScale);
        return Mathf.Clamp(scale, 0.85f, 1.18f);
    }

    private static bool TryParseHeightInches(string value, out float inches)
    {
        inches = 0f;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string input = value.Trim().ToLowerInvariant();

        Match feetInches = Regex.Match(input, "(\\d+)\\s*'\\s*(\\d+)");
        if (feetInches.Success)
        {
            int ft = int.Parse(feetInches.Groups[1].Value);
            int inch = int.Parse(feetInches.Groups[2].Value);
            inches = (ft * 12f) + inch;
            return true;
        }

        Match feetOnly = Regex.Match(input, "(\\d+)\\s*'");
        if (feetOnly.Success)
        {
            int ft = int.Parse(feetOnly.Groups[1].Value);
            inches = ft * 12f;
            return true;
        }

        Match footWord = Regex.Match(input, "(\\d+)\\s*(foot|feet)\\s*(\\d+)?");
        if (footWord.Success)
        {
            int ft = int.Parse(footWord.Groups[1].Value);
            int inch = 0;
            if (footWord.Groups.Count > 3 && !string.IsNullOrWhiteSpace(footWord.Groups[3].Value))
                int.TryParse(footWord.Groups[3].Value, out inch);
            inches = (ft * 12f) + inch;
            return true;
        }

        Match cm = Regex.Match(input, "(\\d+(?:\\.\\d+)?)\\s*cm");
        if (cm.Success)
        {
            float centimeters = float.Parse(cm.Groups[1].Value);
            inches = centimeters / 2.54f;
            return true;
        }

        return false;
    }

    private static List<SuspectProfileSO> BuildOrderedSuspectList(CaseDefinitionSO caseData)
    {
        List<SuspectProfileSO> result = new List<SuspectProfileSO>(SuspectCount);
        if (caseData == null || caseData.suspects == null)
            return result;

        for (int i = 0; i < caseData.suspects.Length && result.Count < SuspectCount; i++)
        {
            SuspectProfileSO s = caseData.suspects[i];
            if (s != null)
                result.Add(s);
        }

        return result;
    }

    public void SelectSuspect(int index)
    {
        selectedSuspectIndex = index;
        UpdateSelectionUI();
        OpenInterviewPopup(index);
    }

    public void ResetSelection()
    {
        selectedSuspectIndex = -1;
        CloseInterviewPopup();
        CloseVerdictSelectionOverlay();
        UpdateSelectionUI();
    }

    private void UpdateSelectionUI()
    {
        string detailTitle = "Case File";
        string detailBody = BuildCaseFileBody(currentCaseData);

        for (int i = 0; i < suspectViews.Length; i++)
        {
            suspectViews[i].SetHighlight(false);
        }

        SetText(selectedSuspectText, string.Empty);
        SetText(detailedInfoTitleText, detailTitle);
        SetText(detailedInfoBodyText, detailBody);

        if (confirmButton != null)
            confirmButton.interactable = currentCaseData != null;
    }

    private static string BuildCaseFileBody(CaseDefinitionSO caseData)
    {
        if (caseData == null)
            return "Summary\nCase details unavailable.\n\nObjective\nInterview suspects, inspect the evidence, and make your accusation.";

        string location = BuildCaseLocation(caseData);
        string summary = string.IsNullOrWhiteSpace(caseData.caseDescription)
            ? "Case summary unavailable."
            : caseData.caseDescription.Trim();

        return "Location\n" + location + "\n\n"
             + "Objective\nInterview suspects, inspect the evidence, and accuse the one person who fits every clue.\n\n"
             + "Brief\n" + summary;
    }

    private static string BuildCaseLocation(CaseDefinitionSO caseData)
    {
        if (caseData == null)
            return "Unknown";

        List<string> parts = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(caseData.locationAddressOrBusiness))
            parts.Add(caseData.locationAddressOrBusiness.Trim());
        if (!string.IsNullOrWhiteSpace(caseData.locationCity))
            parts.Add(caseData.locationCity.Trim());
        if (!string.IsNullOrWhiteSpace(caseData.locationCountry))
            parts.Add(caseData.locationCountry.Trim());

        return parts.Count > 0 ? string.Join("\n", parts) : "Unknown";
    }

        private static string BuildSuspectDialoguePlaceholder(SuspectProfileSO suspect)
        {
            if (suspect == null)
            return "Interview transcript placeholder.\n\n\"Add suspect dialogue lines here.\"";

            string name = string.IsNullOrWhiteSpace(suspect.displayName) ? suspect.name : suspect.displayName;
            string sex = string.IsNullOrWhiteSpace(suspect.sex) ? "Unknown" : suspect.sex;
            string occupation = string.IsNullOrWhiteSpace(suspect.occupation) ? "Unknown" : suspect.occupation;
            string nationality = string.IsNullOrWhiteSpace(suspect.nationality) ? "Unknown" : suspect.nationality;
            string height = string.IsNullOrWhiteSpace(suspect.height) ? "Unknown" : suspect.height;
            string weight = string.IsNullOrWhiteSpace(suspect.weight) ? "Unknown" : suspect.weight;
            string trait = string.IsNullOrWhiteSpace(suspect.keyPersonalityTrait) ? "Unknown" : suspect.keyPersonalityTrait;
            string dialogue = string.IsNullOrWhiteSpace(suspect.dialogue)
                ? "Interview transcript placeholder. Add suspect dialogue lines here."
                : suspect.dialogue;

            string quotedDialogue = "\"" + dialogue + "\"";

            return "Name: " + name + "\n"
                 + "Sex: " + sex + "\n"
                 + "Occupation: " + occupation + "\n"
                 + "Nationality: " + nationality + "\n"
                 + "Height: " + height + "\n"
                 + "Weight: " + weight + "\n"
                 + "Key Personality Trait: " + trait + "\n\n"
                 + quotedDialogue;
        }

    private void ApplyPlaceholders()
    {
        SetText(caseTitleText, "CASE 001");
        SetText(verdictText, "Verdict");
        SetText(explanationText, "Result details appear here after accusation.");
        SetText(detailedInfoTitleText, "Case File");
        SetText(detailedInfoBodyText, BuildCaseFileBody(null));
        ApplySuspectLineupBackground(null);
        currentSuspects.Clear();

        CloseInterviewPopup();

        for (int i = 0; i < suspectViews.Length; i++)
            suspectViews[i].Apply(null, i + 1);

        for (int i = 0; i < evidenceViews.Length; i++)
            evidenceViews[i].Apply(null, i + 1);

        if (resultPanel != null)
            resultPanel.SetActive(false);

        if (verdictText != null && verdictColorCached)
            verdictText.color = verdictDefaultColor;

        UpdateSelectionUI();
    }

    private void AutoBind()
    {
        Transform topBar = FindDeep(transform, "TopBar");
        Transform bottomBar = FindDeep(transform, "BottomBar");
        Transform mainPanel = FindDeep(transform, "MainPanel");
        Transform detailedInfoPanel = FindDeep(transform, "DetailedInfoPanel");

        caseTitleText = topBar != null ? FindText(topBar, "CaseTitleText") : FindText("CaseTitleText");
        suspectsTitleText = mainPanel != null ? FindText(mainPanel, "SuspectsTitleText") : FindText("SuspectsTitleText");
        evidenceTitleText = FindText("EvidenceTitleText");
        selectedSuspectText = bottomBar != null ? FindText(bottomBar, "SelectedSuspectText") : FindText("SelectedSuspectText");
        detailedInfoTitleText = detailedInfoPanel != null ? FindText(detailedInfoPanel, "DetailedInfoTitleText") : null;
        detailedInfoBodyText = detailedInfoPanel != null ? FindText(detailedInfoPanel, "DetailedInfoBodyText") : null;

        if (bottomBar != null)
        {
            HorizontalLayoutGroup bottomLayout = bottomBar.GetComponent<HorizontalLayoutGroup>();
            if (bottomLayout != null)
            {
                bottomLayout.childAlignment = TextAnchor.MiddleRight;
                bottomLayout.childForceExpandWidth = false;
            }
        }

        if (selectedSuspectText != null)
        {
            selectedSuspectText.text = string.Empty;
            selectedSuspectText.gameObject.SetActive(false);

            LayoutElement selectedLayout = selectedSuspectText.GetComponent<LayoutElement>();
            if (selectedLayout != null)
                selectedLayout.ignoreLayout = true;
        }

        Transform resultPanelTransform = FindDeep(transform, "ResultPanel");
        resultPanel = resultPanelTransform != null ? resultPanelTransform.gameObject : null;

        verdictText = resultPanelTransform != null ? FindText(resultPanelTransform, "VerdictText") : FindText("VerdictText");
        explanationText = resultPanelTransform != null ? FindText(resultPanelTransform, "ExplanationText") : FindText("ExplanationText");
        confirmButton = bottomBar != null ? FindButton(bottomBar, "ConfirmButton") : FindButton("ConfirmButton");
        closeVerdictButton = resultPanelTransform != null ? FindButton(resultPanelTransform, "CloseVerdictButton") : FindButton("CloseVerdictButton");

        if (confirmButton != null)
        {
            LayoutElement confirmLayout = confirmButton.GetComponent<LayoutElement>();
            if (confirmLayout != null)
                confirmLayout.ignoreLayout = false;
            confirmButton.gameObject.SetActive(true);
        }

        Button nextCaseButton = FindButton("NextCaseButton");
        HideUiElementForLayout(nextCaseButton != null ? nextCaseButton.gameObject : null);

        HideAllSelectedSuspectLabels();

        if (resultPanel != null && !Application.isPlaying)
            resultPanel.SetActive(false);

        if (verdictText != null && !verdictColorCached)
        {
            verdictDefaultColor = verdictText.color;
            verdictColorCached = true;
        }

        Transform suspectGrid = FindDeep(transform, "SuspectGrid");

        if (suspectGrid != null)
            NormalizeSuspectGridChildren(suspectGrid);

        suspectLineupBackgroundImage = EnsureSuspectLineupBackground(suspectGrid);

        CleanupSuspectActionRow(suspectGrid);

        for (int i = 0; i < suspectViews.Length; i++)
        {
            string name = $"Suspect_{i + 1:00}";
            Transform root = suspectGrid != null ? FindDirectChild(suspectGrid, name) : null;
            if (root == null)
                root = FindDeep(transform, name);

            suspectViews[i] = new SuspectSlotView(root, i, SelectSuspect);
        }

        for (int i = 0; i < evidenceViews.Length; i++)
        {
            string name = $"EvidenceSlot_{i + 1:00}";
            Transform evidenceGrid = FindPreferredEvidenceGrid();
            if (evidenceGrid != null)
                NormalizeEvidenceGridChildren(evidenceGrid);

            Transform root = FindEvidenceSlotFromMainPanel(i + 1);
            if (root == null)
                root = evidenceGrid != null ? FindDirectChild(evidenceGrid, name) : null;
            if (root == null)
                root = FindDeep(transform, name);
            evidenceViews[i] = new EvidenceSlotView(root, OpenEvidencePopup);
        }

        Button resetButton = FindButton("ResetSelectionButton");
        HideUiElementForLayout(resetButton != null ? resetButton.gameObject : null);

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(SubmitAccusation);
            confirmButton.onClick.AddListener(SubmitAccusation);
        }

        if (closeVerdictButton != null)
        {
            closeVerdictButton.onClick.RemoveListener(CloseResultPanel);
            closeVerdictButton.onClick.AddListener(CloseResultPanel);
        }

    }

    private void HideAllSelectedSuspectLabels()
    {
        Text[] texts = GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (text == null || text.name != "SelectedSuspectText")
                continue;

            text.text = string.Empty;
            HideUiElementForLayout(text.gameObject);
        }

        if (detailedInfoTitleText != null && !string.IsNullOrWhiteSpace(detailedInfoTitleText.text))
        {
            string trimmed = detailedInfoTitleText.text.TrimStart();
            if (trimmed.ToLowerInvariant().StartsWith("selected:"))
            {
                string withoutPrefix = trimmed.Substring("selected:".Length).TrimStart();
                detailedInfoTitleText.text = withoutPrefix;
            }
        }
    }

    private static void HideUiElementForLayout(GameObject go)
    {
        if (go == null)
            return;

        LayoutElement layoutElement = go.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = go.AddComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;

        go.SetActive(false);
    }

    private void EnsureEvidenceSlotButtons()
    {
        for (int i = 0; i < EvidenceCount; i++)
        {
            Transform root = FindEvidenceSlotFromMainPanel(i + 1);
            if (root == null)
            {
                string name = $"EvidenceSlot_{i + 1:00}";
                root = FindDeep(transform, name);
            }
            if (root == null)
                continue;

            Button b = root.GetComponent<Button>();
            if (b == null)
            {
                b = root.gameObject.AddComponent<Button>();
                Image target = root.GetComponent<Image>();
                if (target == null)
                    target = root.GetComponentInChildren<Image>();
                if (target != null)
                    b.targetGraphic = target;
                b.transition = Selectable.Transition.ColorTint;
            }
        }
    }

    private void EnsureResultPanelEvidenceImage()
    {
        if (resultPanel == null)
            return;

        Transform existing = FindDeep(resultPanel.transform, "EvidencePopupImage");
        if (existing != null)
        {
            resultPanelEvidenceImage = existing.GetComponent<Image>();
            if (resultPanelEvidenceImage != null)
                resultPanelEvidenceImage.gameObject.SetActive(false);
            return;
        }

        if (!Application.isPlaying)
            return;

        GameObject go = new GameObject("EvidencePopupImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(resultPanel.transform, false);
        rt.anchorMin = new Vector2(0.5f, 0.55f);
        rt.anchorMax = new Vector2(0.5f, 0.55f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(240f, 320f);
        rt.anchoredPosition = Vector2.zero;

        resultPanelEvidenceImage = go.GetComponent<Image>();
        resultPanelEvidenceImage.preserveAspect = true;
        resultPanelEvidenceImage.color = new Color(1f, 1f, 1f, 0f);
        go.SetActive(false);
    }

    private void OpenEvidencePopup(string title, string details, Sprite sprite)
    {
        if (resultPanel == null)
            return;

        EnsureResultPanelEvidenceImage();

        if (verdictText != null)
        {
            verdictText.text = string.IsNullOrWhiteSpace(title) ? "Evidence" : title;
            if (verdictColorCached)
                verdictText.color = verdictDefaultColor;
        }

        if (explanationText != null)
            explanationText.text = string.IsNullOrWhiteSpace(details) ? "No additional notes." : details;

        if (resultPanelEvidenceImage != null)
        {
            resultPanelEvidenceImage.sprite = sprite;
            resultPanelEvidenceImage.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            resultPanelEvidenceImage.gameObject.SetActive(sprite != null);
        }

        resultPanel.SetActive(true);
    }

    public void SubmitAccusation()
    {
        OpenVerdictSelectionOverlay();
    }

    public void CloseResultPanel()
    {
        if (resultPanel != null)
            resultPanel.SetActive(false);

        if (resultPanelEvidenceImage != null)
            resultPanelEvidenceImage.gameObject.SetActive(false);

        if (verdictText != null)
        {
            verdictText.text = currentCaseData != null ? currentCaseData.verdictTitle : "Verdict";
            if (verdictColorCached)
                verdictText.color = verdictDefaultColor;
        }

        if (explanationText != null)
            explanationText.text = currentCaseData != null ? currentCaseData.explanation : "Result details appear here after accusation.";
    }

    private string GetSuspectName(int suspectIndex)
    {
        if (suspectIndex < 0 || suspectIndex >= suspectViews.Length)
            return "Unknown";

        return suspectViews[suspectIndex].GetDisplayName();
    }

    private static void NormalizeSuspectGridChildren(Transform suspectGrid)
    {
        List<Transform> extras = new List<Transform>();

        for (int i = 0; i < suspectGrid.childCount; i++)
        {
            Transform child = suspectGrid.GetChild(i);
            if (child == null)
                continue;

            bool isNamedSuspect = child.name.StartsWith("Suspect_");
            child.gameObject.SetActive(isNamedSuspect);

            LayoutElement le = child.GetComponent<LayoutElement>();
            if (le != null)
                le.ignoreLayout = !isNamedSuspect;

            if (!isNamedSuspect)
                extras.Add(child);
        }

        for (int i = 0; i < SuspectCount; i++)
        {
            string expectedName = $"Suspect_{i + 1:00}";
            Transform slot = FindDirectChild(suspectGrid, expectedName);
            if (slot != null)
                slot.SetSiblingIndex(i);
        }

        for (int i = 0; i < extras.Count; i++)
            extras[i].SetSiblingIndex(SuspectCount + i);
    }

    private Transform FindPreferredEvidenceGrid()
    {
        Transform panel = FindDirectChild(transform, "EvidencePanel");
        if (panel != null)
        {
            Transform grid = FindDirectChild(panel, "EvidenceGrid");
            if (grid != null)
                return grid;
        }

        return null;
    }

    private Transform FindEvidenceSlotFromMainPanel(int oneBasedSlot)
    {
        Transform panel = FindDirectChild(transform, "EvidencePanel");
        if (panel == null)
            return null;

        Transform grid = FindDirectChild(panel, "EvidenceGrid");
        if (grid == null)
            return null;

        return FindDirectChild(grid, $"EvidenceSlot_{oneBasedSlot:00}");
    }

    private static void NormalizeEvidenceGridChildren(Transform evidenceGrid)
    {
        if (evidenceGrid == null)
            return;

        List<Transform> extras = new List<Transform>();
        for (int i = 0; i < evidenceGrid.childCount; i++)
        {
            Transform child = evidenceGrid.GetChild(i);
            if (child == null)
                continue;

            bool isEvidenceSlot = child.name.StartsWith("EvidenceSlot_");
            child.gameObject.SetActive(isEvidenceSlot);

            LayoutElement le = child.GetComponent<LayoutElement>();
            if (le != null)
                le.ignoreLayout = !isEvidenceSlot;

            if (!isEvidenceSlot)
                extras.Add(child);
        }

        for (int i = 0; i < EvidenceCount; i++)
        {
            string expectedName = $"EvidenceSlot_{i + 1:00}";
            Transform slot = FindDirectChild(evidenceGrid, expectedName);
            if (slot != null)
                slot.SetSiblingIndex(i);
        }

        for (int i = 0; i < extras.Count; i++)
            extras[i].SetSiblingIndex(EvidenceCount + i);
    }

    private void ApplyLayoutRules()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ApplyScreenTheme();
            UnityEditor.EditorApplication.delayCall -= DelayedEnsureSuspectGridOneRow;
            UnityEditor.EditorApplication.delayCall += DelayedEnsureSuspectGridOneRow;
            return;
        }
#endif

        EnsureSuspectGridOneRow();
        ApplyScreenTheme();
    }

#if UNITY_EDITOR
    private void DelayedEnsureSuspectGridOneRow()
    {
        if (this == null || !isActiveAndEnabled)
            return;

        EnsureSuspectGridOneRow();
        ApplyScreenTheme();
    }
#endif

    private void ApplyScreenTheme()
    {
        Transform topBar = FindDeep(transform, "TopBar");
        Transform mainPanel = FindDeep(transform, "MainPanel");
        Transform detailedInfoPanel = FindDeep(transform, "DetailedInfoPanel");
        Transform evidencePanel = FindDeep(transform, "EvidencePanel");
        Transform suspectGrid = FindDeep(transform, "SuspectGrid");
        Transform suspectPanel = suspectGrid != null ? suspectGrid.parent : null;
        Transform bottomBar = FindDeep(transform, "BottomBar");

        ApplyTopBarTheme(topBar);
        ApplyMainPanelLayout(mainPanel, suspectPanel, detailedInfoPanel);
        ApplySuspectPanelTheme(suspectPanel, suspectGrid);
        ApplyDetailedInfoTheme(detailedInfoPanel);
        ApplyEvidencePanelTheme(evidencePanel);
        ApplyBottomBarTheme(bottomBar);
    }

    private void ApplyTopBarTheme(Transform topBar)
    {
        RectTransform topBarRect = topBar as RectTransform;
        if (topBarRect != null)
        {
            topBarRect.anchorMin = new Vector2(0.035f, 0.895f);
            topBarRect.anchorMax = new Vector2(0.965f, 0.975f);
            topBarRect.offsetMin = Vector2.zero;
            topBarRect.offsetMax = Vector2.zero;
        }

        Image topBarImage = topBar != null ? topBar.GetComponent<Image>() : null;
        if (topBarImage != null)
            topBarImage.color = new Color(0.03f, 0.035f, 0.045f, 0.94f);

        if (caseTitleText != null)
        {
            caseTitleText.fontSize = 34;
            caseTitleText.fontStyle = FontStyle.Normal;
            caseTitleText.color = new Color(0.98f, 0.95f, 0.88f, 1f);
            caseTitleText.alignment = TextAnchor.MiddleLeft;

            RectTransform titleRect = caseTitleText.transform as RectTransform;
            if (titleRect != null)
            {
                titleRect.anchorMin = new Vector2(0f, 0f);
                titleRect.anchorMax = new Vector2(0.55f, 1f);
                titleRect.offsetMin = new Vector2(22f, 0f);
                titleRect.offsetMax = new Vector2(-12f, 0f);
            }
        }

        Button caseButton = openCaseSelectionButton != null ? openCaseSelectionButton : FindButton("OpenCaseSelectionButton");
        Button menuButton = openMainMenuButton != null ? openMainMenuButton : FindButton("OpenMainMenuButton");
        ApplyButtonTheme(caseButton, new Color(0.36f, 0.29f, 0.16f, 0.92f), new Color(0.98f, 0.95f, 0.86f, 1f), new Vector2(150f, 30f), 14);
        ApplyButtonTheme(menuButton, new Color(0.16f, 0.19f, 0.24f, 0.95f), new Color(0.92f, 0.92f, 0.9f, 1f), new Vector2(104f, 30f), 14);
    }

    private void ApplyMainPanelLayout(Transform mainPanel, Transform suspectPanel, Transform detailedInfoPanel)
    {
        RectTransform mainRect = mainPanel as RectTransform;
        if (mainRect != null)
        {
            mainRect.anchorMin = new Vector2(0.035f, 0.24f);
            mainRect.anchorMax = new Vector2(0.965f, 0.885f);
            mainRect.offsetMin = Vector2.zero;
            mainRect.offsetMax = Vector2.zero;
        }

        RectTransform suspectRect = suspectPanel as RectTransform;
        if (suspectRect != null)
        {
            suspectRect.anchorMin = new Vector2(0f, 0f);
            suspectRect.anchorMax = new Vector2(0.75f, 1f);
            suspectRect.offsetMin = Vector2.zero;
            suspectRect.offsetMax = new Vector2(-18f, 0f);
        }

        RectTransform detailRect = detailedInfoPanel as RectTransform;
        if (detailRect != null)
        {
            detailRect.anchorMin = new Vector2(0.775f, 0.03f);
            detailRect.anchorMax = new Vector2(1f, 1f);
            detailRect.offsetMin = Vector2.zero;
            detailRect.offsetMax = Vector2.zero;
        }
    }

    private void ApplySuspectPanelTheme(Transform suspectPanel, Transform suspectGrid)
    {
        Image panelImage = suspectPanel != null ? suspectPanel.GetComponent<Image>() : null;
        if (panelImage != null)
            panelImage.color = new Color(0.1f, 0.09f, 0.08f, 0.2f);

        if (suspectsTitleText != null)
        {
            suspectsTitleText.text = "Suspect Lineup";
            suspectsTitleText.fontSize = 20;
            suspectsTitleText.fontStyle = FontStyle.Bold;
            suspectsTitleText.color = new Color(0.9f, 0.79f, 0.56f, 0.92f);
            suspectsTitleText.alignment = TextAnchor.MiddleLeft;

            RectTransform titleRect = suspectsTitleText.transform as RectTransform;
            if (titleRect != null)
            {
                titleRect.anchorMin = new Vector2(0f, 1f);
                titleRect.anchorMax = new Vector2(0f, 1f);
                titleRect.pivot = new Vector2(0f, 1f);
                titleRect.anchoredPosition = new Vector2(18f, -16f);
                titleRect.sizeDelta = new Vector2(260f, 28f);
            }
        }

        RectTransform gridRect = suspectGrid as RectTransform;
        if (gridRect != null)
        {
            gridRect.anchorMin = new Vector2(0f, 0f);
            gridRect.anchorMax = new Vector2(1f, 1f);
            gridRect.offsetMin = new Vector2(10f, 12f);
            gridRect.offsetMax = new Vector2(-10f, -44f);
        }

        if (suspectLineupBackgroundImage != null && suspectLineupBackgroundImage.sprite != null)
            suspectLineupBackgroundImage.color = new Color(1f, 1f, 1f, 0.88f);
    }

    private void ApplyDetailedInfoTheme(Transform detailedInfoPanel)
    {
        Image panelImage = detailedInfoPanel != null ? detailedInfoPanel.GetComponent<Image>() : null;
        if (panelImage != null)
            panelImage.color = new Color(0.1f, 0.055f, 0.035f, 0.94f);

        if (detailedInfoTitleText != null)
        {
            detailedInfoTitleText.fontSize = 22;
            detailedInfoTitleText.fontStyle = FontStyle.Bold;
            detailedInfoTitleText.color = new Color(0.98f, 0.93f, 0.84f, 1f);
            detailedInfoTitleText.alignment = TextAnchor.UpperLeft;

            RectTransform titleRect = detailedInfoTitleText.transform as RectTransform;
            if (titleRect != null)
            {
                titleRect.anchorMin = new Vector2(0f, 1f);
                titleRect.anchorMax = new Vector2(1f, 1f);
                titleRect.offsetMin = new Vector2(20f, -56f);
                titleRect.offsetMax = new Vector2(-22f, -16f);
            }
        }

        if (detailedInfoBodyText != null)
        {
            detailedInfoBodyText.fontSize = 15;
            detailedInfoBodyText.fontStyle = FontStyle.Normal;
            detailedInfoBodyText.color = new Color(0.94f, 0.9f, 0.82f, 0.95f);
            detailedInfoBodyText.alignment = TextAnchor.UpperLeft;
            detailedInfoBodyText.lineSpacing = 1.22f;

            RectTransform bodyRect = detailedInfoBodyText.transform as RectTransform;
            if (bodyRect != null)
            {
                bodyRect.anchorMin = new Vector2(0f, 0f);
                bodyRect.anchorMax = new Vector2(1f, 1f);
                bodyRect.offsetMin = new Vector2(20f, 20f);
                bodyRect.offsetMax = new Vector2(-24f, -76f);
            }
        }
    }

    private void ApplyEvidencePanelTheme(Transform evidencePanel)
    {
        RectTransform evidenceRect = evidencePanel as RectTransform;
        if (evidenceRect != null)
        {
            evidenceRect.anchorMin = new Vector2(0.035f, 0.035f);
            evidenceRect.anchorMax = new Vector2(0.73f, 0.205f);
            evidenceRect.offsetMin = Vector2.zero;
            evidenceRect.offsetMax = Vector2.zero;
        }

        Image panelImage = evidencePanel != null ? evidencePanel.GetComponent<Image>() : null;
        if (panelImage != null)
            panelImage.color = new Color(0.06f, 0.08f, 0.1f, 0.92f);

        if (evidenceTitleText != null)
        {
            evidenceTitleText.text = "Evidence";
            evidenceTitleText.fontSize = 16;
            evidenceTitleText.fontStyle = FontStyle.Bold;
            evidenceTitleText.color = new Color(0.88f, 0.78f, 0.58f, 1f);
            evidenceTitleText.alignment = TextAnchor.UpperLeft;

            RectTransform titleRect = evidenceTitleText.transform as RectTransform;
            if (titleRect != null)
            {
                titleRect.anchorMin = new Vector2(0f, 1f);
                titleRect.anchorMax = new Vector2(0f, 1f);
                titleRect.pivot = new Vector2(0f, 0f);
                titleRect.anchoredPosition = new Vector2(18f, 10f);
                titleRect.sizeDelta = new Vector2(180f, 24f);
            }
        }
    }

    private void ApplyBottomBarTheme(Transform bottomBar)
    {
        RectTransform bottomRect = bottomBar as RectTransform;
        if (bottomRect != null)
        {
            bottomRect.anchorMin = new Vector2(0.75f, 0.055f);
            bottomRect.anchorMax = new Vector2(0.965f, 0.17f);
            bottomRect.offsetMin = Vector2.zero;
            bottomRect.offsetMax = Vector2.zero;
        }

        Image panelImage = bottomBar != null ? bottomBar.GetComponent<Image>() : null;
        if (panelImage != null)
            panelImage.color = new Color(0.07f, 0.09f, 0.11f, 0.94f);

        if (confirmButton != null)
            ApplyButtonTheme(confirmButton, new Color(0.53f, 0.38f, 0.17f, 0.98f), new Color(1f, 0.96f, 0.88f, 1f), new Vector2(210f, 56f), 18);
    }

    private static void ApplyButtonTheme(Button button, Color backgroundColor, Color textColor, Vector2 size, int fontSize)
    {
        if (button == null)
            return;

        RectTransform rect = button.transform as RectTransform;
        if (rect != null && size.x > 0f && size.y > 0f)
            rect.sizeDelta = size;

        Image image = button.GetComponent<Image>();
        if (image != null)
            image.color = backgroundColor;

        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.color = textColor;
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
        colors.pressedColor = new Color(0.86f, 0.86f, 0.86f, 0.9f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.45f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null && child.name == name)
                return child;
        }

        return null;
    }

    private void EnsureSuspectGridOneRow()
    {
        Transform suspectGrid = FindDeep(transform, "SuspectGrid");
        if (suspectGrid == null)
            return;

        GridLayoutGroup grid = suspectGrid.GetComponent<GridLayoutGroup>();
        if (grid == null)
            return;

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = SuspectCount;
        grid.spacing = new Vector2(0f, 8f);
        grid.childAlignment = TextAnchor.LowerCenter;
        grid.padding = new RectOffset(8, 8, 8, 8);

        RectTransform rt = suspectGrid as RectTransform;
        if (rt != null && rt.rect.width > 10f)
        {
            float availableWidth = rt.rect.width - grid.padding.left - grid.padding.right - (grid.spacing.x * (SuspectCount - 1));
            float cellWidth = Mathf.Max(120f, availableWidth / SuspectCount);
            float cellHeight = cellWidth * (16f / 9f);

            grid.cellSize = new Vector2(cellWidth, cellHeight);

            if (Application.isPlaying)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
        }

        for (int i = 0; i < suspectViews.Length; i++)
            suspectViews[i].EnforcePortraitPlaceholderRatio(grid.cellSize);
    }

    private Image EnsureSuspectLineupBackground(Transform suspectGrid)
    {
        if (suspectGrid == null)
            return null;

        Transform container = suspectGrid.parent != null ? suspectGrid.parent : suspectGrid;
        Transform existing = FindDirectChild(container, "LocationBackground");
        GameObject backgroundGo;
        if (existing != null)
        {
            backgroundGo = existing.gameObject;
        }
        else
        {
            backgroundGo = new GameObject("LocationBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            RectTransform backgroundRect = backgroundGo.GetComponent<RectTransform>();
            backgroundRect.SetParent(container, false);
        }

        Image backgroundImage = backgroundGo.GetComponent<Image>();
        backgroundImage.raycastTarget = false;
        backgroundImage.preserveAspect = false;
        backgroundImage.type = Image.Type.Simple;

        LayoutElement layout = backgroundGo.GetComponent<LayoutElement>();
        if (layout != null)
            layout.ignoreLayout = true;

        RectTransform rect = backgroundGo.transform as RectTransform;
        RectTransform targetRect = suspectGrid as RectTransform;
        if (rect != null && targetRect != null)
        {
            rect.anchorMin = targetRect.anchorMin;
            rect.anchorMax = targetRect.anchorMax;
            rect.pivot = targetRect.pivot;
            rect.anchoredPosition = targetRect.anchoredPosition;
            rect.sizeDelta = targetRect.sizeDelta;
            rect.offsetMin = targetRect.offsetMin;
            rect.offsetMax = targetRect.offsetMax;
        }

        backgroundGo.transform.SetSiblingIndex(suspectGrid.GetSiblingIndex());
        suspectGrid.SetSiblingIndex(backgroundGo.transform.GetSiblingIndex() + 1);
        return backgroundImage;
    }

    private void ApplySuspectLineupBackground(Sprite sprite)
    {
        if (suspectLineupBackgroundImage == null)
            return;

        suspectLineupBackgroundImage.sprite = sprite;
        suspectLineupBackgroundImage.color = sprite != null
            ? new Color(1f, 1f, 1f, 0.72f)
            : new Color(0f, 0f, 0f, 0f);
    }

    private void CleanupSuspectActionRow(Transform suspectGrid)
    {
        if (suspectGrid == null)
            return;

        Transform parent = suspectGrid.parent;
        if (parent == null)
            return;

        Transform actionRow = FindDirectChild(parent, "SuspectActionRow");
        if (actionRow == null)
            return;

        if (Application.isPlaying)
            Destroy(actionRow.gameObject);
        else
            DestroyImmediate(actionRow.gameObject);
    }

    private Text FindText(string objectName)
    {
        Transform t = FindDeep(transform, objectName);
        return t != null ? t.GetComponent<Text>() : null;
    }

    private static Text FindText(Transform root, string objectName)
    {
        Transform t = FindDeepLocal(root, objectName);
        return t != null ? t.GetComponent<Text>() : null;
    }

    private Button FindButton(string objectName)
    {
        Transform t = FindDeep(transform, objectName);
        return t != null ? t.GetComponent<Button>() : null;
    }

    private static Button FindButton(Transform root, string objectName)
    {
        Transform t = FindDeepLocal(root, objectName);
        return t != null ? t.GetComponent<Button>() : null;
    }

    private static Transform FindDeep(Transform root, string targetName)
    {
        if (root == null)
            return null;

        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform hit = FindDeep(root.GetChild(i), targetName);
            if (hit != null)
                return hit;
        }

        return null;
    }

    private static Transform FindDeepLocal(Transform root, string targetName)
    {
        if (root == null)
            return null;

        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform hit = FindDeepLocal(root.GetChild(i), targetName);
            if (hit != null)
                return hit;
        }

        return null;
    }

    private static void SetText(Text text, string value)
    {
        if (text != null)
            text.text = value ?? string.Empty;
    }

    private struct SuspectSlotView
    {
        private readonly Transform root;
        private readonly Image cardBackground;
        private readonly Image portrait;
        private readonly Image nameBackground;
        private readonly Text nameText;
        private readonly Text bioText;
        private readonly Button selectButton;
        private readonly Text selectButtonText;
        private readonly GameObject highlight;
        private readonly int index;
        private readonly System.Action<int> onSelect;

        public SuspectSlotView(Transform root, int index, System.Action<int> onSelect)
        {
            this.root = root;
            this.index = index;
            this.onSelect = onSelect;

            cardBackground = root != null ? root.GetComponent<Image>() : null;
            portrait = FindImage(root, "Portrait");
            nameText = FindText(root, "NameText");
            nameBackground = SuspectSlotView.EnsureNameBackground(root, nameText);
            bioText = FindText(root, "BioText");
            selectButton = FindButton(root, "SelectButton");
            selectButtonText = selectButton != null ? selectButton.GetComponentInChildren<Text>(true) : null;
            highlight = FindTransform(root, "HighlightFrame")?.gameObject;

            if (selectButtonText != null)
            {
                selectButtonText.text = "Interview";
                selectButtonText.alignment = TextAnchor.MiddleCenter;

                RectTransform selectLabelRect = selectButtonText.transform as RectTransform;
                if (selectLabelRect != null)
                {
                    selectLabelRect.anchorMin = Vector2.zero;
                    selectLabelRect.anchorMax = Vector2.one;
                    selectLabelRect.offsetMin = Vector2.zero;
                    selectLabelRect.offsetMax = Vector2.zero;
                }
            }

            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(HandleSelectClick);
            }
        }

        public void Apply(SuspectProfileSO profile, int fallbackIndex)
        {
            if (root == null)
                return;

            if (profile == null)
            {
                if (cardBackground != null)
                    cardBackground.color = new Color(0f, 0f, 0f, 0f);

                if (portrait != null)
                {
                    portrait.sprite = null;
                    portrait.color = new Color(0f, 0f, 0f, 0f);
                    portrait.preserveAspect = true;
                    RectTransform portraitRect = portrait.transform as RectTransform;
                    if (portraitRect != null)
                        portraitRect.localScale = Vector3.one;
                }

                if (nameText != null)
                    nameText.text = "Suspect " + fallbackIndex;

                if (bioText != null)
                    bioText.text = "Add suspect profile.";

                return;
            }

            if (portrait != null)
            {
                portrait.sprite = profile.portrait;
                portrait.color = profile.portrait != null ? Color.white : new Color(0f, 0f, 0f, 0f);
                portrait.preserveAspect = true;
                float heightScale = CaseScreenController.ComputeHeightScale(profile.height);
                RectTransform portraitRect = portrait.transform as RectTransform;
                if (portraitRect != null)
                    portraitRect.localScale = new Vector3(heightScale, heightScale, 1f);
            }

            if (cardBackground != null)
                cardBackground.color = new Color(0f, 0f, 0f, 0f);

            if (nameText != null)
            {
                nameText.alignment = TextAnchor.MiddleCenter;
                nameText.text = string.IsNullOrWhiteSpace(profile.displayName) ? profile.name : profile.displayName;
            }

            if (bioText != null)
                bioText.text = profile.occupation;
        }

        public void SetHighlight(bool enabled)
        {
            if (highlight != null)
                highlight.SetActive(enabled);
        }

        public string GetDisplayName()
        {
            return nameText != null ? nameText.text : "Unknown";
        }

        public void EnforcePortraitPlaceholderRatio(Vector2 cellSize)
        {
            if (portrait == null)
                return;

            RectTransform portraitRect = portrait.transform as RectTransform;
            if (portraitRect != null)
            {
                portraitRect.anchorMin = new Vector2(0.5f, 0f);
                portraitRect.anchorMax = new Vector2(0.5f, 0f);
                portraitRect.pivot = new Vector2(0.5f, 0f);

                float portraitHeight = Mathf.Max(120f, cellSize.y - 46f);
                float portraitWidth = portraitHeight * (9f / 16f);
                float maxWidth = Mathf.Max(70f, cellSize.x - 12f);
                if (portraitWidth > maxWidth)
                {
                    portraitWidth = maxWidth;
                    portraitHeight = portraitWidth * (16f / 9f);
                }

                portraitRect.sizeDelta = new Vector2(portraitWidth, portraitHeight);
                portraitRect.anchoredPosition = new Vector2(0f, 8f);
            }

            LayoutElement le = portrait.GetComponent<LayoutElement>();
            if (le != null)
                le.ignoreLayout = true;

            RectTransform nameRect = nameText != null ? nameText.transform as RectTransform : null;
            if (nameRect != null)
            {
                nameRect.anchorMin = new Vector2(0f, 0f);
                nameRect.anchorMax = new Vector2(1f, 0f);
                nameRect.offsetMin = new Vector2(8f, 46f);
                nameRect.offsetMax = new Vector2(-8f, 70f);
            }

            RectTransform nameBackgroundRect = nameBackground != null ? nameBackground.transform as RectTransform : null;
            if (nameBackgroundRect != null)
            {
                nameBackgroundRect.anchorMin = new Vector2(0f, 0f);
                nameBackgroundRect.anchorMax = new Vector2(1f, 0f);
                nameBackgroundRect.offsetMin = new Vector2(4f, 42f);
                nameBackgroundRect.offsetMax = new Vector2(-4f, 72f);
            }

            if (nameBackground != null)
            {
                nameBackground.color = new Color(0.03f, 0.04f, 0.05f, 0.72f);
                nameBackground.raycastTarget = false;
                nameBackground.gameObject.SetActive(true);
            }

            if (nameText != null)
            {
                nameText.alignment = TextAnchor.MiddleCenter;
                nameText.gameObject.SetActive(true);
                LayoutElement nameLayout = nameText.GetComponent<LayoutElement>();
                if (nameLayout != null)
                    nameLayout.ignoreLayout = false;
            }

            if (bioText != null)
                bioText.gameObject.SetActive(false);

            RectTransform buttonRect = selectButton != null ? selectButton.transform as RectTransform : null;
            if (buttonRect != null)
            {
                buttonRect.anchorMin = new Vector2(0.5f, 0f);
                buttonRect.anchorMax = new Vector2(0.5f, 0f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.sizeDelta = new Vector2(120f, 30f);
                buttonRect.anchoredPosition = new Vector2(0f, 16f);
            }

            if (selectButton != null)
            {
                LayoutElement buttonLayout = selectButton.GetComponent<LayoutElement>();
                if (buttonLayout != null)
                    buttonLayout.ignoreLayout = false;
                selectButton.gameObject.SetActive(true);
                CaseScreenController.ApplyButtonTheme(selectButton, new Color(0.48f, 0.34f, 0.15f, 0.96f), new Color(0.98f, 0.95f, 0.86f, 1f), new Vector2(120f, 30f), 15);
            }
        }

        private void HandleSelectClick()
        {
            onSelect?.Invoke(index);
        }

        private static Transform FindTransform(Transform root, string name)
        {
            if (root == null)
                return null;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == name)
                    return child;
            }

            return null;
        }

        private static Image FindImage(Transform root, string name)
        {
            Transform t = FindTransform(root, name);
            return t != null ? t.GetComponent<Image>() : null;
        }

        private static Image EnsureNameBackground(Transform root, Text nameText)
        {
            if (root == null || nameText == null)
                return null;

            Transform existing = FindTransform(root, "NameBackground");
            GameObject backgroundGo;
            if (existing != null)
            {
                backgroundGo = existing.gameObject;
            }
            else
            {
                backgroundGo = new GameObject("NameBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform backgroundRect = backgroundGo.GetComponent<RectTransform>();
                backgroundRect.SetParent(root, false);
            }

            Image backgroundImage = backgroundGo.GetComponent<Image>();
            if (backgroundImage == null)
                backgroundImage = backgroundGo.AddComponent<Image>();

            backgroundImage.color = new Color(0.04f, 0.06f, 0.1f, 0.86f);
            backgroundImage.raycastTarget = false;

            RectTransform nameRect = nameText.transform as RectTransform;
            RectTransform backgroundRectCurrent = backgroundGo.transform as RectTransform;
            if (nameRect != null && backgroundRectCurrent != null)
            {
                backgroundRectCurrent.anchorMin = nameRect.anchorMin;
                backgroundRectCurrent.anchorMax = nameRect.anchorMax;
                backgroundRectCurrent.pivot = nameRect.pivot;
                backgroundRectCurrent.offsetMin = nameRect.offsetMin + new Vector2(-4f, -4f);
                backgroundRectCurrent.offsetMax = nameRect.offsetMax + new Vector2(4f, 4f);
            }

            backgroundGo.transform.SetSiblingIndex(nameText.transform.GetSiblingIndex());
            nameText.transform.SetSiblingIndex(backgroundGo.transform.GetSiblingIndex() + 1);
            return backgroundImage;
        }

        private static Text FindText(Transform root, string name)
        {
            Transform t = FindTransform(root, name);
            return t != null ? t.GetComponent<Text>() : null;
        }

        private static Button FindButton(Transform root, string name)
        {
            Transform t = FindTransform(root, name);
            return t != null ? t.GetComponent<Button>() : null;
        }
    }

    private struct EvidenceSlotView
    {
        private readonly Transform root;
        private readonly Image image;
        private readonly Text label;
        private readonly Text description;
        private readonly Button examineButton;
        private readonly System.Action<string, string, Sprite> onExamine;

        public EvidenceSlotView(Transform root, System.Action<string, string, Sprite> onExamine)
        {
            this.root = root;
            this.onExamine = onExamine;
            image = FindImage(root, "EvidenceImage");
            label = FindText(root, "EvidenceLabel");
            description = FindText(root, "EvidenceDescription");

            examineButton = root != null ? root.GetComponent<Button>() : null;

            if (examineButton != null)
            {
                examineButton.onClick.RemoveAllListeners();
                examineButton.onClick.AddListener(OnExamineClicked);
            }
        }

        public void Apply(EvidenceProfileSO profile, int fallbackIndex)
        {
            if (root == null)
                return;

            if (profile == null)
            {
                if (image != null)
                {
                    image.sprite = null;
                    image.color = new Color(0.45f, 0.55f, 0.65f, 1f);
                }

                if (label != null)
                    label.text = "Evidence " + fallbackIndex;

                if (description != null)
                    description.text = "Add evidence profile.";

                return;
            }

            if (image != null)
            {
                image.sprite = profile.image;
                image.color = profile.image != null ? Color.white : new Color(0.45f, 0.55f, 0.65f, 1f);
                image.preserveAspect = true;
            }

            if (label != null)
                label.text = profile.title;

            if (description != null)
                description.text = CaseScreenController.BuildEvidenceDescription(profile);

            CaseScreenController.ApplyEvidenceSlotLayout(root, label, description, image);
        }

        private void OnExamineClicked()
        {
            string title = label != null ? label.text : "Evidence";
            string details = description != null ? description.text : "No additional notes.";
            Sprite sprite = image != null ? image.sprite : null;
            onExamine?.Invoke(title, details, sprite);
        }

        private static Transform FindTransform(Transform root, string name)
        {
            if (root == null)
                return null;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == name)
                    return child;
            }

            return null;
        }

        private static Image FindImage(Transform root, string name)
        {
            Transform t = FindDeepLocal(root, name);
            return t != null ? t.GetComponent<Image>() : null;
        }

        private static Text FindText(Transform root, string name)
        {
            Transform t = FindDeepLocal(root, name);
            return t != null ? t.GetComponent<Text>() : null;
        }

        private static Transform FindDeepLocal(Transform root, string name)
        {
            if (root == null)
                return null;

            if (root.name == name)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindDeepLocal(root.GetChild(i), name);
                if (hit != null)
                    return hit;
            }

            return null;
        }
    }
}
