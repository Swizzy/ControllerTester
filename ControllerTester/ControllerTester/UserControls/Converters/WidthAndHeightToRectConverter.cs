//
// WidthAndHeightToRectConverter.cs
// ControllerTester
//
// Created by Swizzy 16/08/2015
// Copyright (c) 2015 Swizzy. All rights reserved.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace ControllerTester.UserControls.Converters {

    public class WidthAndHeightToRectConverter : MarkupExtension, IMultiValueConverter {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            if (values[0] is double && values[1] is double)
                return new Rect(0, 0, (double)values[0], (double)values[1]);
            return new Rect();
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider) { return this; }
    }

}