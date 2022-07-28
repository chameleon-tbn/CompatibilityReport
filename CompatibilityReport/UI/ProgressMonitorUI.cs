using System;
using ColossalFramework;
using ColossalFramework.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CompatibilityReport.UI
{
    public class ProgressMonitorUI : UIPanel
    {
        private const int PANEL_WIDTH = 650;
        private const int PANEL_HEIGHT = 400;
        private UILabel _title;
        private UIButton _closeButton;
        private UIDragHandle _dragHandle;
        private UIScrollablePanel _logPanel;

        private UIProgressBar _progressBar;
        private UILabel _progressLabel;
        private UILabel _titleLabel;
        private UILabel _stageLabel;
        private UILabel _estimatedTimeLabel;
        private UILabel _elapsedLabel;
        private UILabel _messageTemplate;

        public string Title
        {
            set { _title.text = value ?? string.Empty; }
        }

        public float Progress
        {
            set { _progressBar.value = value; }
        }

        public string ProgressTitle
        {
            set
            {
                if (_titleLabel.text != value)
                {
                    _titleLabel.text = value;
                }
            }
        }

        public string ProgressText
        {
            set
            {
                if (_progressLabel.text != value)
                {
                    _progressLabel.text = value;
                }
            }
        }

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

        public void Initialize()
        {
            UIPanel panel = AddUIComponent<UIPanel>();
            panel.autoLayout = false;
            panel.width = PANEL_WIDTH;
            panel.height = 90;
            panel.relativePosition = new Vector3(0, 40);
            CreateProgressBar(panel);
            absolutePosition = new Vector3(Screen.width / 2 - 100, Screen.height / 2 - 100);
            CreateLogPanel();
        }

        private void CreateLogPanel()
        {
            UIPanel panel = AddUIComponent<UIPanel>();
            panel.size = new Vector2(PANEL_WIDTH, 200);
            panel.autoLayout = true;
            panel.autoLayoutDirection = LayoutDirection.Horizontal;
            panel.relativePosition = new Vector3(0, 185);
            
            _logPanel = panel.AddUIComponent<UIScrollablePanel>();
            _logPanel.builtinKeyNavigation = true;
            _logPanel.autoLayoutDirection = LayoutDirection.Vertical;
            _logPanel.scrollWheelDirection = UIOrientation.Vertical;
            _logPanel.relativePosition = new Vector3(0, 0);
            _logPanel.autoLayoutPadding = new RectOffset(15, 15, 0, 0);
            _logPanel.size = new Vector2(PANEL_WIDTH-20, 200);
            _logPanel.autoLayout = true;
            _logPanel.clipChildren = true;
            
            UIScrollbar scrollbar = panel.AddUIComponent<UIScrollbar>();
            scrollbar.orientation = UIOrientation.Vertical;
            scrollbar.size = new Vector2(20, 200);
            scrollbar.incrementAmount = 30;
            scrollbar.scrollEasingType = EasingType.BackEaseOut;
            scrollbar.scrollEasingTime = 2;
            scrollbar.scrollSize = PANEL_WIDTH;
            
            UISlicedSprite track = scrollbar.AddUIComponent<UISlicedSprite>();
            track.relativePosition = new Vector3(0, 0);
            track.size = new Vector3(18, 200); 
            track.spriteName = "ScrollbarTrack";
            scrollbar.trackObject = track;
            
            UISlicedSprite thumb = track.AddUIComponent<UISlicedSprite>();
            thumb.relativePosition = new Vector3(1, 0);
            thumb.spriteName = "ScrollbarThumb";
            thumb.size = new Vector2(18, 40);
            scrollbar.thumbObject = thumb;
            
            _logPanel.verticalScrollbar = scrollbar;
        }

        public void ResetUI()
        {
            _title.text = "No Title";
            _titleLabel.text = string.Empty;
            _progressBar.value = 0;
            _progressLabel.text = "Processing...";
        }

        public void PushMessage(string message)
        {
            if (!_messageTemplate)
            {
                _messageTemplate = new GameObject("ProgressMonitorUI_Message").AddComponent<UILabel>();
                _messageTemplate.autoHeight = true;
                _messageTemplate.minimumSize = new Vector2(PANEL_WIDTH, 15);
                _messageTemplate.width = PANEL_WIDTH - 50;
                _messageTemplate.text = string.Empty;
                _messageTemplate.textScale = 0.9f;
                _messageTemplate.wordWrap = true;
                _messageTemplate.processMarkup = true;
            }

            GameObject messageObject = Object.Instantiate(_messageTemplate.gameObject);
            UILabel messageLabel = _logPanel.AttachUIComponent(messageObject) as UILabel;
            messageLabel.relativePosition = new Vector3(0, _logPanel.components.Count * 20);
            messageLabel.text = message;
            _logPanel.ScrollToBottom();
        }

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
            _title.text = $"No Title";
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

        private void CreateProgressBar(UIPanel progressPanel)
        {
            _titleLabel = progressPanel.AddUIComponent<UILabel>();
            _titleLabel.padding = new RectOffset(15, 10, 5, 10);
            _titleLabel.relativePosition = new Vector2(0, 0);
            _titleLabel.processMarkup = true;
            _titleLabel.text = string.Empty;

            _progressBar = progressPanel.AddUIComponent<UIProgressBar>();
            _progressBar.relativePosition = new Vector3(15, 55);
            _progressBar.width = PANEL_WIDTH - 30;
            _progressBar.height = 20;
            _progressBar.fillMode = UIFillMode.Fill;
            _progressBar.progressSprite = "ProgressBarFill";
            _progressBar.isVisible = true;
            _progressBar.minValue = 0f;
            _progressBar.maxValue = 1f;
            _progressBar.value = 0f;

            _progressLabel = progressPanel.AddUIComponent<UILabel>();
            _progressLabel.textScale = 0.8f;
            _progressLabel.padding = new RectOffset(15, 10, 10, 15);
            _progressLabel.relativePosition = new Vector2(0, 75);
            _progressLabel.processMarkup = true;
            _progressLabel.text = "Processing...";

            _elapsedLabel = progressPanel.AddUIComponent<UILabel>();
            _elapsedLabel.relativePosition = new Vector2(15, 105);
            _elapsedLabel.text = "N/A";
            _elapsedLabel.prefix = "Elapsed: ";
            _elapsedLabel.Hide();
            _stageLabel = progressPanel.AddUIComponent<UILabel>();
            _stageLabel.relativePosition = new Vector2(PANEL_WIDTH - 110, 105);
            _stageLabel.prefix = "Stage ";
            _stageLabel.Hide();
            _estimatedTimeLabel = progressPanel.AddUIComponent<UILabel>();
            _estimatedTimeLabel.relativePosition = new Vector2(15, 125);
            _estimatedTimeLabel.prefix = "ETA: ";
            _estimatedTimeLabel.text = "";
            _estimatedTimeLabel.Hide();
        }

        private void CloseButtonClick(UIComponent component, UIMouseEventParameter eventparam)
        {
            eventparam.Use();
            OnClose(false);
        }

        private void OnClose(bool resetOnly = true)
        {
            Hide();
            UIComponent modalComponent = UIView.GetModalComponent();
            if (modalComponent == this)
            {
                UIView.PopModal();
            }
            if (!resetOnly)
            {
                Destroy(this.gameObject, 2);
            }
        }

        public void ForceClose()
        {
            OnClose(false);
        }

        public void UpdateElapsedTime(TimeSpan elapsedTime)
        {
            if (elapsedTime > TimeSpan.Zero)
            {
                _elapsedLabel.text = $"{elapsedTime.Hours:D2}h:{elapsedTime.Minutes:D2}m:{elapsedTime.Seconds:D2}s";
                _elapsedLabel.Show();
            }
            else
            {
                _elapsedLabel.Hide();
                _elapsedLabel.text = string.Empty;
            }
        }

        public void UpdateEstimatedTime(double estimatedMilliseconds)
        {
            if (estimatedMilliseconds == 0)
            {
                _estimatedTimeLabel.text = string.Empty;
                _estimatedTimeLabel.Hide();
            }
            else
            {
                _estimatedTimeLabel.text = $"{DateTime.Now.AddMilliseconds(estimatedMilliseconds + 30 * 1000):HH:mm}";
                _estimatedTimeLabel.Show();
            }
        }

        public void UpdateStage(int current, int of)
        {
            if (of == 0)
            {
                _stageLabel.Hide();
            }
            else
            {
                _stageLabel.Show();
                _stageLabel.text = $"{current} of {of}";
            }
        }
    }
}
