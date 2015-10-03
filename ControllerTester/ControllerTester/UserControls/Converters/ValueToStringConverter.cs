//
// ValueToStringConverter.cs
// ControllerTester
//
// Created by Swizzy 16/08/2015
// Copyright (c) 2015 Swizzy. All rights reserved.

using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace ControllerTester.UserControls.Converters {

    public class ValueToStringConverter : MarkupExtension, IValueConverter {
        public ValueToStringConverter() {
            //To hide a stupid error
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return string.Format("{0}", value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider) { return this; }
    }

}