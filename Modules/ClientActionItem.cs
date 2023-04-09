using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TownOfHost
{
    public class ClientActionItem
    {
        public ToggleButtonBehaviour ToggleButton { get; private set; }
        public Action OnClickAction { get; protected set; }

        public static SpriteRenderer CustomBackground { get; private set; }
        private static int numItems = 0;

        protected ClientActionItem(
            string name,
            OptionsMenuBehaviour optionsMenuBehaviour)
        {
            try
            {
                var mouseMoveToggle = optionsMenuBehaviour.DisableMouseMovement;

                // 1つ目のボタンの生成時に背景も生成
                if (CustomBackground == null)
                {
                    numItems = 0;
                    CustomBackground = Object.Instantiate(optionsMenuBehaviour.Background, optionsMenuBehaviour.transform);
                    CustomBackground.name = "CustomBackground";
                    CustomBackground.transform.localScale = new(0.9f, 0.9f, 1f);
                    CustomBackground.transform.localPosition += Vector3.back * 8;
                    CustomBackground.gameObject.SetActive(false);

                    var closeButton = Object.Instantiate(mouseMoveToggle, CustomBackground.transform);
                    closeButton.transform.localPosition = new(1.3f, -2.3f, -6f);
                    closeButton.name = "Close";
                    closeButton.Text.text = Translator.GetString("Close");
                    closeButton.Background.color = Palette.DisabledGrey;
                    var closePassiveButton = closeButton.GetComponent<PassiveButton>();
                    closePassiveButton.OnClick = new();
                    closePassiveButton.OnClick.AddListener(new Action(() =>
                    {
                        CustomBackground.gameObject.SetActive(false);
                    }));

                    UiElement[] selectableButtons = optionsMenuBehaviour.ControllerSelectable.ToArray();
                    PassiveButton leaveButton = null;
                    PassiveButton returnButton = null;
                    for (int i = 0; i < selectableButtons.Length; i++)
                    {
                        var button = selectableButtons[i];
                        if (button == null)
                        {
                            continue;
                        }

                        if (button.name == "LeaveGameButton")
                        {
                            leaveButton = button.GetComponent<PassiveButton>();
                        }
                        else if (button.name == "ReturnToGameButton")
                        {
                            returnButton = button.GetComponent<PassiveButton>();
                        }
                    }
                    var generalTab = mouseMoveToggle.transform.parent.parent.parent;

                    var modOptionsButton = Object.Instantiate(mouseMoveToggle, generalTab);
                    modOptionsButton.transform.localPosition = leaveButton?.transform?.localPosition ?? new(0f, -2.4f, 1f);
                    modOptionsButton.name = "TOHOptions";
                    modOptionsButton.Text.text = Translator.GetString("TOHOptions");
                    modOptionsButton.Background.color = new Color32(0x00, 0xbf, 0xff, 0xff);
                    var modOptionsPassiveButton = modOptionsButton.GetComponent<PassiveButton>();
                    modOptionsPassiveButton.OnClick = new();
                    modOptionsPassiveButton.OnClick.AddListener(new Action(() =>
                    {
                        CustomBackground.gameObject.SetActive(true);
                    }));

                    if (leaveButton != null)
                    {
                        leaveButton.transform.localPosition = new(-1.35f, -2.411f, -1f);
                    }
                    if (returnButton != null)
                    {
                        returnButton.transform.localPosition = new(1.35f, -2.411f, -1f);
                    }
                }

                // ボタン生成
                ToggleButton = Object.Instantiate(mouseMoveToggle, CustomBackground.transform);
                ToggleButton.transform.localPosition = new Vector3(
                    // 現在のオプション数を基に位置を計算
                    numItems % 2 == 0 ? -1.3f : 1.3f,
                    2.2f - (0.5f * (numItems / 2)),
                    -6f);
                ToggleButton.name = name;
                ToggleButton.Text.text = Translator.GetString(name);
                var passiveButton = ToggleButton.GetComponent<PassiveButton>();
                passiveButton.OnClick = new();
                passiveButton.OnClick.AddListener((Action)OnClick);
            }
            finally
            {
                numItems++;
            }
        }

        public static ClientActionItem Create(
            string name,
            Action onClickAction,
            OptionsMenuBehaviour optionsMenuBehaviour)
        {
            return new(name, optionsMenuBehaviour)
            {
                OnClickAction = onClickAction
            };
        }

        public void OnClick()
        {
            OnClickAction?.Invoke();
        }
    }
}
