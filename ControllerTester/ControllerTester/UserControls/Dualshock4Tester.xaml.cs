using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ControllerTester.Backend;

namespace ControllerTester.UserControls {

    /// <summary>
    ///     Interaction logic for Dualshock4Tester.xaml
    /// </summary>
    public partial class Dualshock4Tester {
        private bool _haveDefaults;
        private Dualshock4.Dualshock4Controller _last;
        private double _lsLeft, _lsTop, _rsLeft, _rsTop, _tpLeft, _tpTop;
        private DateTime _lastUpdate;

        public Dualshock4Tester() {
            InitializeComponent();
            var bw = new BackgroundWorker();
            bw.DoWork += DevUpdater;
            bw.RunWorkerAsync();
            GyroBarX.Maximum = GyroBarY.Maximum = GyroBarZ.Maximum = short.MaxValue;
            GyroBarX.Minimum = GyroBarY.Minimum = GyroBarZ.Minimum = short.MinValue;
            AccelBarX.Maximum = AccelBarY.Maximum = AccelBarZ.Maximum = short.MaxValue;
            AccelBarX.Minimum = AccelBarY.Minimum = AccelBarZ.Minimum = short.MinValue;
            L2State.Maximum = R2State.Maximum = byte.MaxValue;
            L2State.Minimum = R2State.Minimum = byte.MinValue;
            LightbarR.Maximum = LightbarG.Maximum = LightbarB.Maximum = byte.MaxValue;
            LightbarR.Minimum = LightbarG.Minimum = LightbarB.Minimum = byte.MinValue;
            Reset();
            ControllersBox.SelectionChanged += ControllersBoxOnSelectionChanged;
        }

        private void ControllersBoxOnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if(_last != null)
                _last.Updated -= Updated;
            _last = ControllersBox.SelectedItem as Dualshock4.Dualshock4Controller;
            if(_last != null)
                _last.Updated += Updated;
        }

        private void Updated(object sender, Ds4UpdateEventArgs e) {
            if(!e.State.IsOk)
                return; // nothing to do here
            Dispatcher.Invoke(
                              new Action(
                                  () => {
                                      if (e.State.Options && HandleLightbarRumble.IsChecked == true) {
                                          if (e.State.Circle)
                                              _last.BacklightColor = Color.FromRgb((byte)(_last.BacklightColor.R + 1), _last.BacklightColor.G, _last.BacklightColor.B);
                                          if (e.State.Triangle)
                                              _last.BacklightColor = Color.FromRgb(_last.BacklightColor.R, (byte)(_last.BacklightColor.G + 1), _last.BacklightColor.B);
                                          if (e.State.Cross)
                                              _last.BacklightColor = Color.FromRgb(_last.BacklightColor.R, _last.BacklightColor.G, (byte)(_last.BacklightColor.B + 1));
                                          //Console.WriteLine("Red: 0x{0:X2} Green: 0x{1:X2} Blue: 0x{2:X2}", _last.BacklightColor.R, _last.BacklightColor.G, _last.BacklightColor.B);
                                          if(e.State.L2 || e.State.R2)
                                              _last.Rumble = new Dualshock4.RumbleStrength(e.State.R2State, e.State.L2State);
                                          else if(_last.Rumble.SmallMotor > 0 || _last.Rumble.BigMotor > 0)
                                              _last.Rumble = new Dualshock4.RumbleStrength(0, 0);
                                      }
                                      else if (_last.Rumble.SmallMotor > 0 || _last.Rumble.BigMotor > 0)
                                          _last.Rumble = new Dualshock4.RumbleStrength(0, 0);
                                      LightbarR.Value = _last.BacklightColor.R;
                                      LightbarG.Value = _last.BacklightColor.G;
                                      LightbarB.Value = _last.BacklightColor.B;
                                      LeftRumble.Value = Math.Round(_last.Rumble.BigMotor / 2.55, 2);
                                      RightRumble.Value = Math.Round(_last.Rumble.SmallMotor / 2.55, 2);
                                      BacklightCanvas.Background = new SolidColorBrush(_last.BacklightColor);
                                      TouchpadPolygon.Visibility = e.State.TouchPadButton
                                                                       ? Visibility.Visible : Visibility.Hidden;
                                      SharePolygon.Visibility = e.State.Share ? Visibility.Visible : Visibility.Hidden;
                                      OptionsPolygon.Visibility = e.State.Options
                                                                      ? Visibility.Visible : Visibility.Hidden;
                                      DpadLeftPolygon.Visibility = e.State.DpadLeft
                                                                       ? Visibility.Visible : Visibility.Hidden;
                                      DpadRightPolygon.Visibility = e.State.DpadRight
                                                                        ? Visibility.Visible : Visibility.Hidden;
                                      DpadUpPolygon.Visibility = e.State.DpadUp ? Visibility.Visible : Visibility.Hidden;
                                      DpadDownPolygon.Visibility = e.State.DpadDown
                                                                       ? Visibility.Visible : Visibility.Hidden;
                                      PsEllipse.Visibility = e.State.PlaystationButton
                                                                 ? Visibility.Visible : Visibility.Hidden;
                                      TriangleEllipse.Visibility = e.State.Triangle
                                                                       ? Visibility.Visible : Visibility.Hidden;
                                      SquareEllipse.Visibility = e.State.Square ? Visibility.Visible : Visibility.Hidden;
                                      CrossEllipse.Visibility = e.State.Cross ? Visibility.Visible : Visibility.Hidden;
                                      CircleEllipse.Visibility = e.State.Circle ? Visibility.Visible : Visibility.Hidden;
                                      L1Polygon.Visibility = e.State.L1 ? Visibility.Visible : Visibility.Hidden;
                                      R1Polygon.Visibility = e.State.R1 ? Visibility.Visible : Visibility.Hidden;
                                      LeftStickEllipse.Visibility = e.State.L3 ? Visibility.Hidden : Visibility.Visible;
                                      RightStickEllipse.Visibility = e.State.R3 ? Visibility.Hidden : Visibility.Visible;

                                      #region Left Stick

                                      if(e.State.LeftY < 127)
                                          LeftStickEllipse.SetValue(
                                                                    Canvas.TopProperty,
                                                                    _lsTop -
                                                                    ((Math.Abs((128 - (double)e.State.LeftY)) / 128) *
                                                                     15));
                                      else if(e.State.LeftY > 128)
                                          LeftStickEllipse.SetValue(
                                                                    Canvas.TopProperty,
                                                                    _lsTop +
                                                                    ((Math.Abs((128 - (double)e.State.LeftY)) / 128) *
                                                                     15));
                                      else
                                          LeftStickEllipse.SetValue(Canvas.TopProperty, _lsTop);
                                      if(e.State.LeftX < 127)
                                          LeftStickEllipse.SetValue(
                                                                    Canvas.LeftProperty,
                                                                    _lsLeft -
                                                                    ((Math.Abs((128 - (double)e.State.LeftX)) / 128) *
                                                                     15));
                                      else if(e.State.LeftX > 128)
                                          LeftStickEllipse.SetValue(
                                                                    Canvas.LeftProperty,
                                                                    _lsLeft +
                                                                    ((Math.Abs((128 - (double)e.State.LeftX)) / 128) *
                                                                     15));
                                      else
                                          LeftStickEllipse.SetValue(Canvas.LeftProperty, _lsLeft);

                                      #endregion


                                      #region Right Stick

                                      if(e.State.RightY < 127)
                                          RightStickEllipse.SetValue(
                                                                     Canvas.TopProperty,
                                                                     _rsTop -
                                                                     ((Math.Abs((128 - (double)e.State.RightY)) / 128) *
                                                                      15));
                                      else if(e.State.RightY > 128)
                                          RightStickEllipse.SetValue(
                                                                     Canvas.TopProperty,
                                                                     _rsTop +
                                                                     ((Math.Abs((128 - (double)e.State.RightY)) / 128) *
                                                                      15));
                                      else
                                          RightStickEllipse.SetValue(Canvas.TopProperty, _rsTop);
                                      if(e.State.RightX < 127)
                                          RightStickEllipse.SetValue(
                                                                     Canvas.LeftProperty,
                                                                     _rsLeft -
                                                                     ((Math.Abs((128 - (double)e.State.RightX)) / 128) *
                                                                      15));
                                      else if(e.State.RightX > 128)
                                          RightStickEllipse.SetValue(
                                                                     Canvas.LeftProperty,
                                                                     _rsLeft +
                                                                     ((Math.Abs((128 - (double)e.State.RightX)) / 128) *
                                                                      15));
                                      else
                                          RightStickEllipse.SetValue(Canvas.LeftProperty, _rsLeft);

                                      #endregion

                                      Touch1.Visibility = e.State.TouchPad.IsP1Active
                                                              ? Visibility.Visible : Visibility.Hidden;
                                      Touch1.SetValue(Canvas.LeftProperty, _tpLeft + (((double)e.State.TouchPad.TouchX1 / 2048) * 95));
                                      Touch1.SetValue(Canvas.TopProperty, _tpTop + (((double)e.State.TouchPad.TouchY1 / 1000) * 32));

                                      Touch2.Visibility = e.State.TouchPad.IsP2Active
                                                              ? Visibility.Visible : Visibility.Hidden;
                                      Touch2.SetValue(Canvas.LeftProperty, _tpLeft + (((double)e.State.TouchPad.TouchX2 / 2048) * 95));
                                      Touch2.SetValue(Canvas.TopProperty, _tpTop + (((double)e.State.TouchPad.TouchY2 / 1000) * 32));


                                      GyroBarX.Value = e.State.GyroX;
                                      GyroBarY.Value = e.State.GyroY;
                                      GyroBarZ.Value = e.State.GyroZ;
                                      AccelBarX.Value = e.State.AccelX;
                                      AccelBarY.Value = e.State.AccelY;
                                      AccelBarZ.Value = e.State.AccelZ;
                                      BatteryLevel.Value = e.State.BatteryLevel;
                                      L2State.Value = e.State.L2State;
                                      R2State.Value = e.State.R2State;
                                  }));
        }

        private void Reset() {
            GyroBarX.Value = GyroBarY.Value = GyroBarZ.Value = short.MinValue;
            AccelBarX.Value = AccelBarY.Value = AccelBarZ.Value = short.MinValue;
            BatteryLevel.Value = 0;
            if(!_haveDefaults) {
                _haveDefaults = true;
                _lsLeft = (double)LeftStickEllipse.GetValue(Canvas.LeftProperty);
                _lsTop = (double)LeftStickEllipse.GetValue(Canvas.TopProperty);

                _rsLeft = (double)RightStickEllipse.GetValue(Canvas.LeftProperty);
                _rsTop = (double)RightStickEllipse.GetValue(Canvas.TopProperty);

                _tpLeft = (double)Touch1.GetValue(Canvas.LeftProperty);
                _tpTop = (double)Touch1.GetValue(Canvas.TopProperty);
            }
            LeftStickEllipse.SetValue(Canvas.LeftProperty, _lsLeft);
            LeftStickEllipse.SetValue(Canvas.TopProperty, _lsTop);

            RightStickEllipse.SetValue(Canvas.LeftProperty, _rsLeft);
            RightStickEllipse.SetValue(Canvas.TopProperty, _rsTop);

            LeftStickEllipse.Visibility = RightStickEllipse.Visibility = Visibility.Visible;
            Touch1.Visibility = Touch2.Visibility = Visibility.Hidden;
            BacklightCanvas.Background = new SolidColorBrush(Color.FromRgb(0, 0, 0xFF));
            LightbarR.Value = LightbarG.Value = 0;
            LightbarB.Value = 0xFF;
            LeftRumble.Value = RightRumble.Value = 0;
            DpadDownPolygon.Visibility = DpadUpPolygon.Visibility = Visibility.Hidden;
            DpadLeftPolygon.Visibility = DpadRightPolygon.Visibility = Visibility.Hidden;
            TouchpadPolygon.Visibility = PsEllipse.Visibility = Visibility.Hidden;
            SquareEllipse.Visibility = TriangleEllipse.Visibility = Visibility.Hidden;
            CrossEllipse.Visibility = CircleEllipse.Visibility = Visibility.Hidden;
            OptionsPolygon.Visibility = SharePolygon.Visibility = Visibility.Hidden;
            L1Polygon.Visibility = R1Polygon.Visibility = Visibility.Hidden;
        }

        private void DevUpdater(object sender, DoWorkEventArgs doWorkEventArgs) {
            while(true) {
                var prev = ControllersBox.Items.Count;
                var controllers = Dualshock4.GetControllers();
                if(prev == controllers.Length)
                    if(DateTime.Now.Subtract(_lastUpdate).TotalSeconds <= 1)
                        continue;
                Dispatcher.Invoke(
                                  new Action(
                                      () => {
                                          var sel = ControllersBox.SelectedItem;
                                          ControllersBox.ItemsSource = controllers;
                                          if(sel != null && controllers.Contains(sel))
                                              ControllersBox.SelectedItem = sel;
                                          else if(ControllersBox.Items.Count > 0) {
                                              Reset();
                                              ControllersBox.SelectedIndex = 0;
                                          }
                                          else
                                              Reset();
                                          _lastUpdate = DateTime.Now;
                                      }));
            }
        }
    }

}