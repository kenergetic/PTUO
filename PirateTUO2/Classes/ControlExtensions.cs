using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PirateTUO2.Classes
{
    /// <summary>
    /// Class to have threads update winform controls
    /// 
    /// Winform is single threaded and does not like any other threads touching it. This is a workaround of sorts
    /// http://stackoverflow.com/questions/783925/control-invoke-with-input-parameters
    /// </summary>
    public static class ControlExtensions
    {
        public static TResult InvokeEx<TControl, TResult>(this TControl control,
                                                   Func<TControl, TResult> func)
          where TControl : Control
        {
            return control.InvokeRequired
                    ? (TResult)control.Invoke(func, control)
                    : func(control);
        }

        public static void InvokeEx<TControl>(this TControl control,
                                              Action<TControl> func)
          where TControl : Control
        {
            control.InvokeEx(c => { func(c); return c; });
        }

        public static void InvokeEx<TControl>(this TControl control, Action action)
          where TControl : Control
        {
            control.InvokeEx(c => action());
        }
    }
}
