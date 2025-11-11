using System;
using UnityEngine.UIElements;

namespace PrefabPreview
{
    [UxmlElement]
    public partial class TimeSlider : VisualElement
    {
        private readonly FloatSlider _slider;
        private readonly Label _label;

        public event Action<float> OnValueChanged;

        [UxmlAttribute("min")]
        public float Min
        {
            get => _slider.Min;
            set => _slider.Min = value;
        }

        [UxmlAttribute("max")]
        public float Max
        {
            get => _slider.Max;
            set => _slider.Max = value;
        }

        [UxmlAttribute("value")]
        public float Value
        {
            get => _slider.Value;
            set
            {
                _slider.Value = value;
                if (_label != null) SetTime();
            }
        }

        public TimeSlider()
        {
            _slider = new FloatSlider { style = { flexGrow = 1 } };
            _label = new Label { style = { marginLeft = 4 } };
            Add(_slider);
            Add(_label);
            SetTime();
            _slider.OnValueChanged += value =>
            {
                SetTime();
                OnValueChanged?.Invoke(value);
            };
        }

        private void SetTime()
        {
            _label.text = $"{Value:0.00} / {Max:0.00} Sec";
        }
    }
}