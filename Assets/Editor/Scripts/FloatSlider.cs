#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrefabPreview
{
    [UxmlElement]
    public partial class FloatSlider : VisualElement
    {
        private readonly Slider _slider;
        private readonly FloatField _field;
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
                _value = Mathf.Clamp(value, _min, _max);
                if (_slider != null) _slider.SetValueWithoutNotify(_value);
                if (_field != null) _field.SetValueWithoutNotify(_value);
            }
        }

        public FloatSlider()
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;

            _slider = new Slider { style = { flexGrow = 1 } };
            _field = new FloatField { style = { width = 60, marginLeft = 4 } };

            Add(_slider);
            Add(_field);

            _slider.lowValue = _min;
            _slider.highValue = _max;
            _slider.value = _value;
            _field.value = _value;
            _field.formatString = "0.00";
            _slider.RegisterValueChangedCallback(evt =>
            {
                _value = evt.newValue;
                _field.SetValueWithoutNotify(evt.newValue);
                OnValueChanged?.Invoke(_value);
            });
            _field.RegisterValueChangedCallback(evt =>
            {
                _value = Mathf.Clamp(evt.newValue, _min, _max);
                _slider.SetValueWithoutNotify(_value);
                _field.SetValueWithoutNotify(_value);
                OnValueChanged?.Invoke(_value);
            });
        }
    }
}
#endif