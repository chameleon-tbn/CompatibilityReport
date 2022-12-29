#if CATALOG_DOWNLOAD
using System.Diagnostics;
using ColossalFramework.UI;
using CompatibilityReport.Settings;
using CompatibilityReport.Util;
using UnityEngine;
using Logger = CompatibilityReport.Util.Logger;

namespace CompatibilityReport.UI
{
    public class UploadCatalogUI : UIPanel
    {
        private const int PANEL_WIDTH = 305;
        private const int PANEL_HEIGHT = 300;
        private UILabel _title;
        private UIButton _closeButton;
        private UIDragHandle _dragHandle;
        private UILabel _statusLabel;
        
        public override void Awake()
        {
            base.Awake();
            autoLayout = false;
            width = PANEL_WIDTH;
            height = PANEL_HEIGHT;
            backgroundSprite = "UnlockingPanel2";
            color = new Color32(72, 104, 139, 255);
            CreateTitle();
            CreateDragHandle();
            CreateCloseButton();
        }
        
        public override void OnDestroy()
        {
            base.OnDestroy();
            //it's already destroying itself no need to destroy gameobject again  
            OnClose(resetOnly: true);
        }
        
        public void Initialize() {
            UIPanel panel = AddUIComponent<UIPanel>();
            panel.autoLayout = false;
            panel.width = PANEL_WIDTH;
            panel.height = 90;
            panel.relativePosition = new Vector3(0, 40);
            CreateLoginPanelContent(panel);
        }

        private void CreateLoginPanelContent(UIPanel progressPanel) {
            UIHelper helperInput = new UIHelper(progressPanel);
            var loginTextField = helperInput.AddTextfield("Login", string.Empty, EmptyAction, EmptyAction) as UITextField;
            var passTextField = helperInput.AddTextfield("Password", string.Empty, EmptyAction, EmptyAction)as UITextField;
            loginTextField.size = new Vector2(265, loginTextField.size.y);
            passTextField.isPasswordField = true;
            passTextField.size = new Vector2(265, loginTextField.size.y);
            progressPanel.autoLayoutDirection = LayoutDirection.Vertical;
            progressPanel.autoLayoutPadding = new RectOffset(20, 15, 0, 10);
            progressPanel.autoLayout = true;

            UIPanel helperPanel = progressPanel.AddUIComponent<UIPanel>();
            var helper = new UIHelper(helperPanel);
            helperPanel.autoLayoutDirection = LayoutDirection.Horizontal;
            helperPanel.autoLayoutPadding = new RectOffset(0, 5, 0, 0);
            helperPanel.size = new Vector2(progressPanel.size.x, 40);
            helperPanel.relativePosition = Vector3.zero;
            helperPanel.autoLayout = true;
            helper.AddButton("Upload", () => 
                SettingsManager.OnUploadCatalog(
                    loginTextField.text, 
                    passTextField.text, 
                    b => UpdateLabel(b ? "Success!":"Failure!"))
            );
            helper.AddButton("Open directory", () => Process.Start(ModSettings.UpdaterPath));

            UIButton refreshButton = helperInput.AddButton("Refresh file status", () => {
                UpdateLabel();
            }) as UIButton;
            refreshButton.textHorizontalAlignment = UIHorizontalAlignment.Center;
            refreshButton.autoSize = false;
            refreshButton.size = new Vector2(270, refreshButton.size.y); 

            _statusLabel = progressPanel.AddUIComponent<UILabel>();
            _statusLabel.textScale = 0.8f;
            _statusLabel.relativePosition = new Vector2(0, 75);
            _statusLabel.processMarkup = true;
            UpdateLabel();
        }

        private void UpdateLabel(string otherText = null) {
            _statusLabel.text = !string.IsNullOrEmpty(otherText)
                    ? otherText
                    : $"{ModSettings.UploadCatalogFileName} <color {(ModSettings.UploadFileAvailable ? "#00ff00>Found" : "#ff0000>Not Found!")}</color>";
        }
        
        private void EmptyAction(string s) {}
        
        private void CreateDragHandle()
        {
            _dragHandle = AddUIComponent<UIDragHandle>();
            _dragHandle.area = new Vector4(0, 0, PANEL_WIDTH - 40, 45);
        }

        private void CreateTitle()
        {
            _title = AddUIComponent<UILabel>();
            _title.autoSize = true;
            _title.textScale = 1.2f;
            _title.padding = new RectOffset(0, 10, 5, 15);
            _title.relativePosition = new Vector2(20, 8);
            _title.text = "Upload Catalog";
            _title.textAlignment = UIHorizontalAlignment.Center;
        }

        private void CreateCloseButton()
        {
            _closeButton = AddUIComponent<UIButton>();
            _closeButton.eventClick += CloseButtonClick;
            _closeButton.relativePosition = new Vector3(width - _closeButton.width - 35, 5f);
            _closeButton.normalBgSprite = "buttonclose";
            _closeButton.hoveredBgSprite = "buttonclosehover";
            _closeButton.pressedBgSprite = "buttonclosepressed";
        }
        
        private void CloseButtonClick(UIComponent component, UIMouseEventParameter eventparam)
        {
            eventparam.Use();
            OnClose(false);
        }
        
        private void OnClose(bool resetOnly = true)
        {
            UIComponent modalComponent = UIView.GetModalComponent();
            Hide();
            if (modalComponent == this)
            {
                UIView.PopModal();
            }
            if (!resetOnly)
            {
                Destroy(this.gameObject, 2);
            }
        }
    }
}
#endif
