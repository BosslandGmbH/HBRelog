using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Serialization;
using HighVoltz.HBRelog.Converters;
using HighVoltz.HBRelog.Tasks;

namespace HighVoltz.HBRelog.Controls
{
    /// <summary>
    /// Interaction logic for TaskEditor.xaml
    /// </summary>
    public partial class TaskEditor : UserControl
    {
        public TaskEditor()
        {
            InitializeComponent();
        }

        object _source;
        public object Source
        {
            get { return _source; }
            set { _source = value; NotifyPropertyChanged("Source"); SourceChanged(value); }
        }

        void SourceChanged(object source)
        {
            if (!(source is BMTask))
                throw new InvalidOperationException("Can only assign a BMTask derived class to Source");
            BMTask task = (BMTask)source;

            List<PropertyInfo> propertyList = task.GetType().GetProperties().
                Where(pi => pi.GetCustomAttributesData().All(cad => cad.Constructor.DeclaringType != typeof(XmlIgnoreAttribute))).ToList();
            PropertyGrid.Children.Clear();
            PropertyGrid.RowDefinitions.Clear();
            for (int index = 0; index < propertyList.Count; index++)
            {
                PropertyGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(22) });
                var propNameText = new TextBlock()
                {
                    Text = SpacifierConverter.GetSpaciousString(propertyList[index].Name),
                    Margin = new Thickness(2, 0, 2, 0)
                };
                Grid.SetRow(propNameText, index);
                PropertyGrid.Children.Add(propNameText);

                Control propEdit;
                // check if the property has CustomTaskEditControl attribute attached
                CustomTaskEditControlAttribute customControlAttr =
                    (CustomTaskEditControlAttribute)propertyList[index].GetCustomAttributes(typeof(CustomTaskEditControlAttribute), false).FirstOrDefault();
                if (customControlAttr != null)
                {
                    propEdit = (Control)Activator.CreateInstance(customControlAttr.ControlType);
                    if (!(propEdit is ICustomTaskEditControlDataBound))
                        throw new InvalidOperationException("CustomTaskEditControl must implement the ICustomTaskEditControlDataBound interface");
                    ((ICustomTaskEditControlDataBound)propEdit).SetValue(propertyList[index].GetValue(task, null));
                    ((ICustomTaskEditControlDataBound)propEdit).SetBinding(task, propertyList[index].Name);
                }
                else // no custom controls attached to property so load the default control for the property type
                    propEdit = GetControlForType(propertyList[index].PropertyType, propertyList[index].GetValue(task, null));
                propEdit.Margin = new Thickness(0, 1, 0, 1);
                propEdit.Tag = new { Task = task, Property = propertyList[index] };

                Grid.SetColumn((UIElement)propEdit, 1);
                Grid.SetRow((UIElement)propEdit, index);
                PropertyGrid.Children.Add((UIElement)propEdit);
            }

        }
        private Control GetCustomControl(Type type, object value)
        {
            Control ctrl = null;
            return ctrl;
        }

        private Control GetControlForType(Type type, object value)
        {
            Control ctrl = null;
            if (type == typeof(bool))
            {
                ctrl = new ComboBox();
                ((ComboBox)ctrl).Items.Add(true);
                ((ComboBox)ctrl).Items.Add(false);
                ((ComboBox)ctrl).SelectedItem = value;
                ((ComboBox)ctrl).SelectionChanged += TaskPropertyChanged;
            }
            else if (value is Enum)
            {
                ctrl = new ComboBox();
                foreach (object val in Enum.GetValues(type))
                    ((ComboBox)ctrl).Items.Add(val);
                ((ComboBox)ctrl).SelectedItem = value;
                ((ComboBox)ctrl).SelectionChanged += TaskPropertyChanged;
            }
            else
            {
                ctrl = new TextBox();
                if (value != null)
                    ((TextBox)ctrl).Text = value.ToString();
                ((TextBox)ctrl).TextChanged += TaskPropertyChanged;
            }
            return ctrl;
        }

        void TaskPropertyChanged(object sender, EventArgs e)
        {
            BMTask task = ((dynamic)((Control)sender).Tag).Task;
            PropertyInfo pi = ((dynamic)((Control)sender).Tag).Property;
            if (sender is ComboBox)
            {
                pi.SetValue(task, ((ComboBox)sender).SelectedValue, null);
            }
            else if (sender is TextBox)
            {
                string str = ((TextBox)sender).Text;
                try
                {
                    object val = Convert.ChangeType(str, pi.PropertyType);
                    pi.SetValue(task, val, null);
                } // in case the type conversion fails fall back to default value.
                catch (FormatException)
                {
                    object defaultValue = GetDefaultValue(pi.PropertyType);
                   pi.SetValue(task, defaultValue, null);
                }
            }
            ((RoutedEventArgs)e).Handled = true;
        }

        public object GetDefaultValue(Type t)
        {
            if (t.IsValueType)
            {
                return Activator.CreateInstance(t);
            }
            else
            {
                return null;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        #region Embedded Type - CustomTaskEditControlAttribute
        [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
        public sealed class CustomTaskEditControlAttribute : Attribute
        {
            public CustomTaskEditControlAttribute(Type controlType)
            {
                this.ControlType = controlType;
            }
            public Type ControlType { get; private set; }
        }
        #endregion

        #region Embedded Type - ICustomTaskEditControlDataBound
        public interface ICustomTaskEditControlDataBound
        {
            void SetBinding(BMTask source, string path);
            void SetValue(object value);
        }
        #endregion
    }
}
