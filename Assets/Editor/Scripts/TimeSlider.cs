#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrefabPreview
{
    [UxmlElement]
    public partial class TimeSlider : VisualElement
    {
        private readonly FloatSlider _slider;
        private readonly Label _label;
        private readonly FloatField _max;

        public event Action<float> OnValueChanged;
        public event Action<float> OnMaxChanged;

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
            set
            {
                _slider.Max = value;
                _max.value = value;
            }
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
            var bottom = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            _max = new FloatField
            {
                formatString = "0.00"
            };
            _max.RegisterValueChangedCallback(evt =>
            {
                var newMax = Mathf.Max(evt.newValue, 0f);
                _slider.Max = newMax;
                OnMaxChanged?.Invoke(newMax);
                _max.SetValueWithoutNotify(newMax);
            });
            bottom.Add(_label);
            bottom.Add(_max);
            bottom.Add(new Label { text = "Sec", style = { marginLeft = 4 } });
            Add(bottom);
            SetTime();
            _slider.OnValueChanged += value =>
            {
                SetTime();
                OnValueChanged?.Invoke(value);
            };
        }

        private void SetTime()
        {
            _label.text = $"{Value:0.00} /";
        }
    }
}
#endif