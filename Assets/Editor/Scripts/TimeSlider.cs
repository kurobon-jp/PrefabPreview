using System;
using UnityEngine.UIElements;

namespace PrefabPreview
{
    [UxmlElement]
    public partial class TimeSlider : VisualElement
    {
        private readonly Slider _slider;
        private readonly Label _label;

        private float _min;
        private float _max;
        private float _value;

        public event Action<float> OnValueChanged;

        [UxmlAttribute("min")]
        public float Min
        {
            get => _min;
            set
            {
                _min = value;
                if (_slider != null) _slider.lowValue = value;
            }
        }

        [UxmlAttribute("max")]
        public float Max
        {
            get => _max;
            set
            {
                _max = value;
                if (_slider != null) _slider.highValue = value;
            }
        }

        [UxmlAttribute("value")]
        public float Value
        {
            get => _value;
            set
            {
                _value = value;
                if (_slider != null) _slider.SetValueWithoutNotify(value);
                if (_label != null) SetTime();
            }
        }

        public TimeSlider()
        {
            _slider = new Slider { style = { flexGrow = 1 } };
            _label = new Label { style = { marginLeft = 4 } };

            Add(_slider);
            Add(_label);

            _slider.lowValue = _min;
            _slider.highValue = _max;
            _slider.value = _value;
            SetTime();
            _slider.RegisterValueChangedCallback(evt =>
            {
                _value = evt.newValue;
                SetTime();
                OnValueChanged?.Invoke(_value);
            });
        }

        private void SetTime()
        {
            _label.text = $"{_value:0.00} / {_max:0.00} Sec";
        }
    }
}