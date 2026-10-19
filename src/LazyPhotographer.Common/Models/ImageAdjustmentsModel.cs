using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LazyPhotographer.Interop;

namespace LazyPhotographer.Models
{
    public class ImageAdjustmentsModel : INotifyPropertyChanged
    {
        private float _exposure = 0.0f;
        private float _temperature = 0.0f;
        private float _tint = 0.0f;
        private float _contrast = 0.0f;
        private float _highlights = 0.0f;
        private float _shadows = 0.0f;
        private float _whites = 0.0f;
        private float _blacks = 0.0f;
        private float _saturation = 0.0f;
        private float _vibrance = 0.0f;
        private float _sharpness = 0.0f;
        private float _denoise = 0.0f;
        private bool _enableAiAutoTone = false;
        private float _aiDenoiseStrength = 0.0f;
        private bool _enableAiDetailEnhance = false;

        public float Exposure
        {
            get => _exposure;
            set => SetField(ref _exposure, value);
        }

        public float Temperature
        {
            get => _temperature;
            set => SetField(ref _temperature, value);
        }

        public float Tint
        {
            get => _tint;
            set => SetField(ref _tint, value);
        }

        public float Contrast
        {
            get => _contrast;
            set => SetField(ref _contrast, value);
        }

        public float Highlights
        {
            get => _highlights;
            set => SetField(ref _highlights, value);
        }

        public float Shadows
        {
            get => _shadows;
            set => SetField(ref _shadows, value);
        }

        public float Whites
        {
            get => _whites;
            set => SetField(ref _whites, value);
        }

        public float Blacks
        {
            get => _blacks;
            set => SetField(ref _blacks, value);
        }

        public float Saturation
        {
            get => _saturation;
            set => SetField(ref _saturation, value);
        }

        public float Vibrance
        {
            get => _vibrance;
            set => SetField(ref _vibrance, value);
        }

        public float Sharpness
        {
            get => _sharpness;
            set => SetField(ref _sharpness, value);
        }

        public float Denoise
        {
            get => _denoise;
            set => SetField(ref _denoise, value);
        }

        public bool EnableAiAutoTone
        {
            get => _enableAiAutoTone;
            set => SetField(ref _enableAiAutoTone, value);
        }

        public float AiDenoiseStrength
        {
            get => _aiDenoiseStrength;
            set => SetField(ref _aiDenoiseStrength, value);
        }

        public bool EnableAiDetailEnhance
        {
            get => _enableAiDetailEnhance;
            set => SetField(ref _enableAiDetailEnhance, value);
        }

        public NativeAdjustments ToNative()
        {
            return new NativeAdjustments
            {
                Exposure = _exposure,
                Temperature = _temperature,
                Tint = _tint,
                Contrast = _contrast,
                Highlights = _highlights,
                Shadows = _shadows,
                Whites = _whites,
                Blacks = _blacks,
                Saturation = _saturation,
                Vibrance = _vibrance,
                Sharpness = _sharpness,
                Denoise = _denoise,
                EnableAiAutoTone = _enableAiAutoTone ? 1 : 0,
                AiDenoiseStrength = _aiDenoiseStrength,
                EnableAiDetailEnhance = _enableAiDetailEnhance ? 1 : 0
            };
        }

        public void FromNative(NativeAdjustments native)
        {
            Exposure = native.Exposure;
            Temperature = native.Temperature;
            Tint = native.Tint;
            Contrast = native.Contrast;
            Highlights = native.Highlights;
            Shadows = native.Shadows;
            Whites = native.Whites;
            Blacks = native.Blacks;
            Saturation = native.Saturation;
            Vibrance = native.Vibrance;
            Sharpness = native.Sharpness;
            Denoise = native.Denoise;
            EnableAiAutoTone = native.EnableAiAutoTone != 0;
            AiDenoiseStrength = native.AiDenoiseStrength;
            EnableAiDetailEnhance = native.EnableAiDetailEnhance != 0;
        }

        public void Reset()
        {
            Exposure = 0.0f;
            Temperature = 0.0f;
            Tint = 0.0f;
            Contrast = 0.0f;
            Highlights = 0.0f;
            Shadows = 0.0f;
            Whites = 0.0f;
            Blacks = 0.0f;
            Saturation = 0.0f;
            Vibrance = 0.0f;
            Sharpness = 0.0f;
            Denoise = 0.0f;
            EnableAiAutoTone = false;
            AiDenoiseStrength = 0.0f;
            EnableAiDetailEnhance = false;
        }

        public ImageAdjustmentsModel Clone()
        {
            var clone = new ImageAdjustmentsModel();
            clone.FromNative(this.ToNative());
            return clone;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
