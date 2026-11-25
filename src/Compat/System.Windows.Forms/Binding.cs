// WinForms data binding as component settings pages use it: a control property bound to a
// property of the settings object. The control reads the setting when it is shown (or at once
// when added to a shown control), and writes it back whenever the control's value changes
// (DataSourceUpdateMode.OnPropertyChanged) or when asked (WriteValue).
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace System.Windows.Forms;

public class Binding
{
    private Control _control;
    private EventInfo _changedEvent;
    private EventHandler _changedHandler;
    private bool _pushing;

    public Binding(string propertyName, object dataSource, string dataMember)
        : this(propertyName, dataSource, dataMember, false, DataSourceUpdateMode.OnValidation)
    {
    }

    public Binding(string propertyName, object dataSource, string dataMember, bool formattingEnabled)
        : this(propertyName, dataSource, dataMember, formattingEnabled, DataSourceUpdateMode.OnValidation)
    {
    }

    public Binding(string propertyName, object dataSource, string dataMember, bool formattingEnabled, DataSourceUpdateMode dataSourceUpdateMode)
        : this(propertyName, dataSource, dataMember, formattingEnabled, dataSourceUpdateMode, null, "", null)
    {
    }

    public Binding(string propertyName, object dataSource, string dataMember, bool formattingEnabled, DataSourceUpdateMode dataSourceUpdateMode, object nullValue)
        : this(propertyName, dataSource, dataMember, formattingEnabled, dataSourceUpdateMode, nullValue, "", null)
    {
    }

    public Binding(string propertyName, object dataSource, string dataMember, bool formattingEnabled, DataSourceUpdateMode dataSourceUpdateMode, object nullValue, string formatString, IFormatProvider formatInfo)
    {
        PropertyName = propertyName;
        DataSource = dataSource;
        BindingMemberInfo = new BindingMemberInfo(dataMember);
        FormattingEnabled = formattingEnabled;
        DataSourceUpdateMode = dataSourceUpdateMode;
        NullValue = nullValue;
        FormatString = formatString;
        FormatInfo = formatInfo;
    }

    public event ConvertEventHandler Format;

    public event ConvertEventHandler Parse;

    public event BindingCompleteEventHandler BindingComplete;

    public string PropertyName { get; }

    public object DataSource { get; }

    public BindingMemberInfo BindingMemberInfo { get; }

    public Control Control => _control;

    public bool FormattingEnabled { get; set; }

    public DataSourceUpdateMode DataSourceUpdateMode { get; set; }

    public ControlUpdateMode ControlUpdateMode { get; set; }

    public object NullValue { get; set; }

    public object DataSourceNullValue { get; set; }

    public string FormatString { get; set; }

    public IFormatProvider FormatInfo { get; set; }

    public bool IsBinding => _control is not null;

    /// <summary>The control shows the setting.</summary>
    public void ReadValue()
    {
        if (_control is null || ControlProperty is not { CanWrite: true } property)
        {
            return;
        }

        object value = SourceProperty?.GetValue(DataSource);
        var args = new ConvertEventArgs(value, property.PropertyType);
        Format?.Invoke(this, args);
        _pushing = true;
        try
        {
            property.SetValue(_control, ConvertTo(args.Value, property.PropertyType));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidCastException or FormatException or OverflowException or TargetInvocationException)
        {
            BindingComplete?.Invoke(this, new BindingCompleteEventArgs(this, BindingCompleteState.DataError, BindingCompleteContext.ControlUpdate));
        }
        finally
        {
            _pushing = false;
        }
    }

    /// <summary>The setting takes the control's value.</summary>
    public void WriteValue()
    {
        if (_control is null || _pushing || ControlProperty is not { } property || SourceProperty is not { CanWrite: true } source)
        {
            return;
        }

        var args = new ConvertEventArgs(property.GetValue(_control), source.PropertyType);
        Parse?.Invoke(this, args);
        try
        {
            source.SetValue(DataSource, ConvertTo(args.Value, source.PropertyType));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidCastException or FormatException or OverflowException or TargetInvocationException)
        {
            BindingComplete?.Invoke(this, new BindingCompleteEventArgs(this, BindingCompleteState.DataError, BindingCompleteContext.DataSourceUpdate));
        }
    }

    internal void Attach(Control control)
    {
        _control = control;
        // The control's <Property>Changed event (for SelectedItem, SelectedIndexChanged).
        string eventName = PropertyName == "SelectedItem" ? "SelectedIndexChanged" : PropertyName + "Changed";
        _changedEvent = control.GetType().GetEvent(eventName, BindingFlags.Public | BindingFlags.Instance);
        if (_changedEvent?.EventHandlerType == typeof(EventHandler))
        {
            _changedHandler = (_, _) =>
            {
                if (DataSourceUpdateMode == DataSourceUpdateMode.OnPropertyChanged)
                {
                    WriteValue();
                }
            };
            _changedEvent.AddEventHandler(control, _changedHandler);
        }

        if (control.IsShown)
        {
            ReadValue();
        }
    }

    internal void Detach()
    {
        if (_control is not null && _changedHandler is not null)
        {
            _changedEvent?.RemoveEventHandler(_control, _changedHandler);
        }

        _control = null;
    }

    private PropertyInfo ControlProperty => FindProperty(_control?.GetType(), PropertyName);

    private PropertyInfo SourceProperty =>
        FindProperty(DataSource?.GetType(), BindingMemberInfo.BindingField);

    /// <summary>
    /// The public property of that name, the most derived one when a class hides its base's with
    /// "new" (a settings page's float Height over Control's int Height), as WinForms'
    /// TypeDescriptor finds it.
    /// </summary>
    private static PropertyInfo FindProperty(Type type, string name)
    {
        for (Type t = type; t is not null; t = t.BaseType)
        {
            PropertyInfo property = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (property is not null)
            {
                return property;
            }
        }

        return null;
    }

    private static object ConvertTo(object value, Type type)
    {
        if (value is null)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        if (type.IsInstanceOfType(value))
        {
            return value;
        }

        Type target = Nullable.GetUnderlyingType(type) ?? type;
        if (target.IsEnum)
        {
            return value is string text ? Enum.Parse(target, text) : Enum.ToObject(target, value);
        }

        if (target == typeof(string))
        {
            return Convert.ToString(value, CultureInfo.CurrentCulture);
        }

        if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(target))
        {
            return Convert.ChangeType(value, target, CultureInfo.CurrentCulture);
        }

        TypeConverter converter = TypeDescriptor.GetConverter(target);
        return converter.CanConvertFrom(value.GetType()) ? converter.ConvertFrom(value) : value;
    }
}

public struct BindingMemberInfo
{
    public BindingMemberInfo(string dataMember)
    {
        BindingMember = dataMember ?? "";
        int dot = BindingMember.LastIndexOf('.');
        BindingPath = dot >= 0 ? BindingMember[..dot] : "";
        BindingField = dot >= 0 ? BindingMember[(dot + 1)..] : BindingMember;
    }

    public string BindingField { get; }

    public string BindingMember { get; }

    public string BindingPath { get; }
}

public enum ControlUpdateMode
{
    OnPropertyChanged = 0,
    Never = 1,
}

public enum BindingCompleteState
{
    Success = 0,
    DataError = 1,
    Exception = 2,
}

public enum BindingCompleteContext
{
    ControlUpdate = 0,
    DataSourceUpdate = 1,
}

public class BindingCompleteEventArgs : CancelEventArgs
{
    public BindingCompleteEventArgs(Binding binding, BindingCompleteState state, BindingCompleteContext context)
    {
        Binding = binding;
        BindingCompleteState = state;
        BindingCompleteContext = context;
    }

    public Binding Binding { get; }

    public BindingCompleteState BindingCompleteState { get; }

    public BindingCompleteContext BindingCompleteContext { get; }
}

public delegate void BindingCompleteEventHandler(object sender, BindingCompleteEventArgs e);

public class ConvertEventArgs : EventArgs
{
    public ConvertEventArgs(object value, Type desiredType)
    {
        Value = value;
        DesiredType = desiredType;
    }

    public object Value { get; set; }

    public Type DesiredType { get; }
}

public delegate void ConvertEventHandler(object sender, ConvertEventArgs e);

public class BaseCollection : MarshalByRefObject, ICollection
{
    public virtual int Count => List.Count;

    public bool IsReadOnly => false;

    public bool IsSynchronized => false;

    public object SyncRoot => this;

    protected virtual ArrayList List { get; } = [];

    public void CopyTo(Array ar, int index) => List.CopyTo(ar, index);

    public IEnumerator GetEnumerator() => List.GetEnumerator();
}

public class BindingsCollection : BaseCollection
{
    internal BindingsCollection()
    {
    }

    public event CollectionChangeEventHandler CollectionChanged;

    public Binding this[int index] => (Binding)List[index];

    protected internal void Add(Binding binding) => AddCore(binding);

    protected virtual void AddCore(Binding dataBinding)
    {
        List.Add(dataBinding);
        CollectionChanged?.Invoke(this, new CollectionChangeEventArgs(CollectionChangeAction.Add, dataBinding));
    }

    protected internal void Clear() => ClearCore();

    protected virtual void ClearCore()
    {
        List.Clear();
        CollectionChanged?.Invoke(this, new CollectionChangeEventArgs(CollectionChangeAction.Refresh, null));
    }

    protected internal void Remove(Binding binding) => RemoveCore(binding);

    protected virtual void RemoveCore(Binding dataBinding)
    {
        List.Remove(dataBinding);
        CollectionChanged?.Invoke(this, new CollectionChangeEventArgs(CollectionChangeAction.Remove, dataBinding));
    }
}

public class ControlBindingsCollection : BindingsCollection
{
    public ControlBindingsCollection(IBindableComponent control) => BindableComponent = control;

    public IBindableComponent BindableComponent { get; }

    public Control Control => BindableComponent as Control;

    public DataSourceUpdateMode DefaultDataSourceUpdateMode { get; set; }

    public Binding this[string propertyName] =>
        List.Cast<Binding>().FirstOrDefault(b => string.Equals(b.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase));

    public new void Add(Binding binding) => base.Add(binding);

    public Binding Add(string propertyName, object dataSource, string dataMember) =>
        Add(propertyName, dataSource, dataMember, false, DefaultDataSourceUpdateMode, null, "", null);

    public Binding Add(string propertyName, object dataSource, string dataMember, bool formattingEnabled) =>
        Add(propertyName, dataSource, dataMember, formattingEnabled, DefaultDataSourceUpdateMode, null, "", null);

    public Binding Add(string propertyName, object dataSource, string dataMember, bool formattingEnabled, DataSourceUpdateMode updateMode) =>
        Add(propertyName, dataSource, dataMember, formattingEnabled, updateMode, null, "", null);

    public Binding Add(string propertyName, object dataSource, string dataMember, bool formattingEnabled, DataSourceUpdateMode updateMode, object nullValue) =>
        Add(propertyName, dataSource, dataMember, formattingEnabled, updateMode, nullValue, "", null);

    public Binding Add(string propertyName, object dataSource, string dataMember, bool formattingEnabled, DataSourceUpdateMode updateMode, object nullValue, string formatString) =>
        Add(propertyName, dataSource, dataMember, formattingEnabled, updateMode, nullValue, formatString, null);

    public Binding Add(string propertyName, object dataSource, string dataMember, bool formattingEnabled, DataSourceUpdateMode updateMode, object nullValue, string formatString, IFormatProvider formatInfo)
    {
        var binding = new Binding(propertyName, dataSource, dataMember, formattingEnabled, updateMode, nullValue, formatString, formatInfo);
        Add(binding);
        return binding;
    }

    public new void Clear() => base.Clear();

    public new void Remove(Binding binding) => base.Remove(binding);

    public void RemoveAt(int index) => Remove(this[index]);

    protected override void AddCore(Binding dataBinding)
    {
        if (this[dataBinding.PropertyName] is not null)
        {
            throw new ArgumentException($"This causes two bindings in the collection to bind to the same property.", nameof(dataBinding));
        }

        base.AddCore(dataBinding);
        if (Control is { } control)
        {
            dataBinding.Attach(control);
        }
    }

    protected override void ClearCore()
    {
        foreach (Binding binding in List.Cast<Binding>().ToList())
        {
            binding.Detach();
        }

        base.ClearCore();
    }

    protected override void RemoveCore(Binding dataBinding)
    {
        dataBinding.Detach();
        base.RemoveCore(dataBinding);
    }

    internal void ReadAll()
    {
        foreach (Binding binding in List.Cast<Binding>().ToList())
        {
            binding.ReadValue();
        }
    }
}

public interface IBindableComponent : IComponent
{
    ControlBindingsCollection DataBindings { get; }

    BindingContext BindingContext { get; set; }
}

public class BindingContext : ICollection
{
    public int Count => 0;

    public bool IsReadOnly => true;

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => this;

    void ICollection.CopyTo(Array array, int index)
    {
    }

    public IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
}
