using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace Smart3
{
    #region PLU data.
    /// <summary>
    /// Base class representing a price look-up code for an article.
    /// </summary>
    [Serializable]
    public class PLU : INotifyPropertyChanged
    {
        protected const string outOfRangeErrorFormat = "Value exceeded allowable range: {0}–{1} inclusive.";
        protected const string readOnlyError = "Cannot change property while instance is in a read-only state.";
        private const string regEx = @"^[\x20\x21\x23\x25-\x39\x3C\x3E\x3FA-Za-z]{1,13}$"; // If the PLU code is alphanumeric, it can contain ASCII characters from 032 to 127, with the exception of characters 058 and 059.
        private const string regExError = @"String must be 1 through 13 characters long and may contain spaces, !#%&'()*+,-./<>? and alphanumeric characters only.";
        private const int quantityMin = 1;
        private const int quantityMax = 99999;
        private readonly string id;
        private int quantity = 1;
        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            IsChanged = true;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Internal flag for tracking changes to this object during the transaction.
        internal bool IsChanged { get; set; }

        #region Private data validation.
        private bool CheckId(string id)
        {
            return Regex.IsMatch(id, regEx);
        }
        private bool CheckQuantity(int quantity)
        {
            return (quantity >= quantityMin && quantity <= quantityMax);
        }
        #endregion
        #region Public properties.
        /// <summary>
        /// Indicates the state of read-only access to this object.
        /// </summary>
        public bool IsReadOnly { get; internal set; }
        /// <summary>
        /// Indicates a unique alphanumeric price look-up code with maximum length of 13 characters.
        /// </summary>
        public string Id
        {
            get { return id; }
        }
        /// <summary>
        /// Indicates an article sale quantity when used in transactions.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Setting a value that is outside of allowable range.</exception>
        /// <exception cref="InvalidOperationException">Setting a value while object is read-only.</exception>
        public int Quantity
        {
            get { return quantity; }
            set
            {
                if (value != quantity)
                {
                    if (IsReadOnly) throw new InvalidOperationException(readOnlyError);
                    if (!CheckQuantity(value)) throw new ArgumentOutOfRangeException(nameof(value), string.Format(outOfRangeErrorFormat, quantityMin, quantityMax));
                    quantity = value;
                    OnPropertyChanged(nameof(Quantity));
                }
            }
        }
        #endregion
        #region Constructors.
        /// <summary>
        /// Create a new instance of <see cref="PLU"/> with a specified identifier.
        /// </summary>
        /// <param name="id">Unique identifier as price look-up code.</param>
        /// <exception cref="ArgumentNullException">Id is null.</exception>
        /// <exception cref="ArgumentException">Id is empty, too long, or contains invalid characters.</exception>
        public PLU(string id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            if (!CheckId(id)) throw new ArgumentException(regExError, nameof(id));
            this.id = id;
        }
        /// <summary>
        /// Create a new instance of <see cref="PLU"/> with a specified identifier and quantity.
        /// </summary>
        /// <param name="id">Unique identifier of an article as price look-up code.</param>
        /// <param name="quantity">Sale quantity of an article.</param>
        /// <exception cref="ArgumentNullException">Id is null.</exception>
        /// <exception cref="ArgumentException">Id is empty, too long, or contains invalid characters.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Quantity is outside of allowable range.</exception>
        public PLU(string id, int quantity) : this(id)
        {
            if (!CheckQuantity(quantity)) throw new ArgumentOutOfRangeException(nameof(quantity), string.Format(outOfRangeErrorFormat, quantityMin, quantityMax));
            this.quantity = quantity;
        }
        #endregion
    }
    /// <summary>
    /// Class representing a complete information about an article that is stored in a cash register.
    /// </summary>
    [Serializable]
    public sealed class PLUInfo : PLU
    {
        private const string regEx = @"^[\x20\x21\x23\x25-\x39\x3C\x3E\x3FA-Za-z]{1,21}$";
        private const string regExError = @"String must be 1 through 21 characters long and may contain spaces, !#%&'()*+,-./<>? and alphanumeric characters only.";
        private const decimal priceMin = 0.01M;
        private const decimal priceMax = 999999.99M;
        private const int departmentMin = 1;
        private const int departmentMax = 250;
        private const int taxMin = 1;
        private const int taxMax = 9;
        private const int macroMin = 0;
        private const int macroMax = 250;
        private string name;
        private decimal price;
        private int department = 1;
        private int tax = 3;
        private int macro = 0;

        #region Private data validation.
        private bool CheckName(string name)
        {
            return Regex.IsMatch(name, regEx);
        }
        private bool CheckPrice(decimal price)
        {
            return (price >= priceMin && price <= priceMax);
        }
        private bool CheckDepartment(int department)
        {
            return (department >= departmentMin && department <= departmentMax);
        }
        private bool CheckTax(int tax)
        {
            return (tax >= taxMin && tax <= taxMax);
        }
        private bool CheckMacro(int macro)
        {
            return (macro >= macroMin && macro <= macroMax);
        }
        #endregion
        #region Public properties.
        /// <summary>
        /// Indicates an article name with maximum length of 21 characters.
        /// </summary>
        /// <exception cref="ArgumentNullException">Setting a null value.</exception>
        /// <exception cref="ArgumentException">Setting a value that is empty, too long, or contains invalid characters.</exception>
        /// <exception cref="InvalidOperationException">Setting a value while object is read-only.</exception>
        public string Name
        {
            get { return name; }
            set
            {
                if (value != name)
                {

                    if (IsReadOnly) throw new InvalidOperationException(readOnlyError);
                    if (value == null) throw new ArgumentNullException(nameof(value));
                    if (!CheckName(value)) throw new ArgumentException(regExError, nameof(value));
                    name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }
        /// <summary>
        /// Indicates an article sale price.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Setting a value that is outside of allowable range.</exception>
        /// <exception cref="InvalidOperationException">Setting a value while object is read-only.</exception>
        public decimal Price
        {
            get { return price; }
            set
            {
                if (value != price)
                {

                    if (IsReadOnly) throw new InvalidOperationException(readOnlyError);
                    if (!CheckPrice(value)) throw new ArgumentOutOfRangeException(nameof(value), string.Format(outOfRangeErrorFormat, priceMin, priceMax));
                    price = value;
                    OnPropertyChanged(nameof(Price));
                }
            }
        }
        /// <summary>
        /// Indicates a cash register defined department code associated with an article.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Setting a value that is outside of allowable range.</exception>
        /// <exception cref="InvalidOperationException">Setting a value while object is read-only.</exception>
        public int Department
        {
            get { return department; }
            set
            {
                if (value != department)
                {

                    if (IsReadOnly) throw new InvalidOperationException(readOnlyError);
                    if (!CheckDepartment(value)) throw new ArgumentOutOfRangeException(nameof(value), string.Format(outOfRangeErrorFormat, departmentMin, departmentMax));
                    department = value;
                    OnPropertyChanged(nameof(Department));
                }
            }
        }
        /// <summary>
        /// Indicates a cash register defined tax rate code associated with an article.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Setting a value that is outside of allowable range.</exception>
        /// <exception cref="InvalidOperationException">Setting a value while object is read-only.</exception>
        public int Tax
        {
            get { return tax; }
            set
            {
                if (value != tax)
                {

                    if (IsReadOnly) throw new InvalidOperationException(readOnlyError);
                    if (!CheckTax(value)) throw new ArgumentOutOfRangeException(nameof(value), string.Format(outOfRangeErrorFormat, taxMin, taxMax));
                    tax = value;
                    OnPropertyChanged(nameof(Tax));
                }
            }
        }
        /// <summary>
        /// Indicates a cash register defined macro function code associated with an article.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Setting a value that is outside of allowable range.</exception>
        /// <exception cref="InvalidOperationException">Setting a value while object is read-only.</exception>
        public int Macro
        {
            get { return macro; }
            set
            {
                if (value != macro)
                {

                    if (IsReadOnly) throw new InvalidOperationException(readOnlyError);
                    if (!CheckMacro(value)) throw new ArgumentOutOfRangeException(nameof(value), string.Format(outOfRangeErrorFormat, macroMin, macroMax));
                    macro = value;
                    OnPropertyChanged(nameof(Macro));
                }
            }
        }
        #endregion
        #region Constructors.
        #region Base(id) constructor stack.
        /// <summary>
        /// Create new instance of <see cref="PLUInfo"/> with a specified identifier, name, and price.
        /// </summary>
        /// <param name="id">Unique identifier of an article as price look-up code.</param>
        /// <param name="name">Name of an article.</param>
        /// <param name="price">Sale price of an article.</param>
        /// <exception cref="ArgumentNullException">Id or name is null.</exception>
        /// <exception cref="ArgumentException">Id or name is empty, too long, or contains invalid characters.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Price is outside of allowable range.</exception>
        public PLUInfo(string id, string name, decimal price) : base(id)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (!CheckName(name)) throw new ArgumentException(regExError, nameof(name));
            if (!CheckPrice(price)) throw new ArgumentOutOfRangeException(nameof(price), string.Format(outOfRangeErrorFormat, priceMin, priceMax));
            this.name = name;
            this.price = price;
        }
        /// <summary>
        /// Create new instance of <see cref="PLUInfo"/> with a specified identifier, name, price, department, tax, and macro.
        /// </summary>
        /// <param name="id">Unique identifier of an article as price look-up code.</param>
        /// <param name="name">Name of an article.</param>
        /// <param name="price">Sale price of an article.</param>
        /// <param name="department">Department code associated with an article.</param>
        /// <param name="tax">Tax rate code associated with an article.</param>
        /// <param name="macro">Macro function code associated with an article.</param>
        /// <exception cref="ArgumentNullException">Id or name is null.</exception>
        /// <exception cref="ArgumentException">Id or name is empty, too long, or contains invalid characters.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Price, department, tax, or macro is outside of allowable range.</exception>
        public PLUInfo(string id, string name, decimal price, int department, int tax, int macro) : this(id, name, price)
        {
            if (!CheckDepartment(department)) throw new ArgumentOutOfRangeException(nameof(department), string.Format(outOfRangeErrorFormat, departmentMin, departmentMax));
            if (!CheckTax(tax)) throw new ArgumentOutOfRangeException(nameof(tax), string.Format(outOfRangeErrorFormat, taxMin, taxMax));
            if (!CheckMacro(macro)) throw new ArgumentOutOfRangeException(nameof(macro), string.Format(outOfRangeErrorFormat, macroMin, macroMax));
            this.department = department;
            this.tax = tax;
            this.macro = macro;
        }
        #endregion
        #region Base(id, quantity) constructor stack.
        /// <summary>
        /// Create new instance of <see cref="PLUInfo"/> with a specified identifier, name, price, and quantity.
        /// </summary>
        /// <param name="id">Unique identifier of an article as price look-up code.</param>
        /// <param name="name">Name of an article.</param>
        /// <param name="price">Sale price of an article.</param>
        /// <param name="quantity">Sale quantity of an article.</param>
        /// <exception cref="ArgumentNullException">Id or name is null.</exception>
        /// <exception cref="ArgumentException">Id or name is empty, too long, or contains invalid characters.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Price or quantity is outside of allowable range.</exception>
        public PLUInfo(string id, string name, decimal price, int quantity) : base(id, quantity)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (!CheckName(name)) throw new ArgumentException(regExError, nameof(name));
            if (!CheckPrice(price)) throw new ArgumentOutOfRangeException(nameof(price), string.Format(outOfRangeErrorFormat, priceMin, priceMax));
            this.name = name;
            this.price = price;
        }
        /// <summary>
        /// Create new instance of <see cref="PLUInfo"/> with a specified identifier, name, price, department, tax, macro, and quantity.
        /// </summary>
        /// <param name="id">Unique identifier of an article as price look-up code.</param>
        /// <param name="name">Name of an article.</param>
        /// <param name="price">Sale price of an article.</param>
        /// <param name="department">Department code associated with an article.</param>
        /// <param name="tax">Tax rate code associated with an article.</param>
        /// <param name="macro">Macro function code associated with an article.</param>
        /// <param name="quantity">Sale quantity of an article.</param>
        /// <exception cref="ArgumentNullException">Id or name is null.</exception>
        /// <exception cref="ArgumentException">Id or name is empty, too long, or contains invalid characters.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Price, department, tax, macro, or quantity is outside of allowable range.</exception>
        public PLUInfo(string id, string name, decimal price, int department, int tax, int macro, int quantity) : this(id, name, price, quantity)
        {
            if (!CheckDepartment(department)) throw new ArgumentOutOfRangeException(nameof(department), string.Format(outOfRangeErrorFormat, departmentMin, departmentMax));
            if (!CheckTax(tax)) throw new ArgumentOutOfRangeException(nameof(tax), string.Format(outOfRangeErrorFormat, taxMin, taxMax));
            if (!CheckMacro(macro)) throw new ArgumentOutOfRangeException(nameof(macro), string.Format(outOfRangeErrorFormat, macroMin, macroMax));
            this.department = department;
            this.tax = tax;
            this.macro = macro;
        }
        #endregion
        /// <summary>
        /// Create new instance of <see cref="PLUInfo"/> by copying values from other instance.
        /// </summary>
        /// <param name="other">Other instance of <see cref="PLUInfo"/> to copy values from.</param>
        public PLUInfo(PLUInfo other) : this(other.Id, other.Name, other.Price, other.Department, other.Tax, other.Macro, other.Quantity) { }
        #endregion
    }

    /// <summary>
    /// Compares if two <see cref="PLU"/> are of the same Id.
    /// </summary>
    public class PLUComparer<T> : IEqualityComparer<T> where T : PLU
    {
        public bool Equals(T x, T y)
        {
            if (x == y) return true;
            if (x == null || y == null) return false;
            return (x.Id == y.Id);
        }
        public int GetHashCode(T obj)
        {
            return obj.Id.GetHashCode();
        }
    }
    #endregion

    #region Financial data.
    /// <summary>
    /// Cash register's financial report.
    /// </summary>
    public class FinancialReport
    {
        internal FinancialReport() { }
        /// <summary>
        /// Number of tickets issued since last fiscal closing.
        /// </summary>
        public int TicketsIssued { get; internal set; }
        /// <summary>
        /// Number of items sold since last fiscal closing.
        /// </summary>
        public int ItemsSold { get; internal set; }
        /// <summary>
        /// Total amount of money received from payments since last fiscal closing.
        /// </summary>
        public decimal PaymentAmount { get; internal set; }
        /// <summary>
        /// Total amount of money inflow since last fiscal closing.
        /// </summary>
        public decimal InflowAmount { get; internal set; }
        /// <summary>
        /// Total amount of money outflow since last fiscal closing.
        /// </summary>
        public decimal OutflowAmount { get; internal set; }
        /// <summary>
        /// Total amount of money in drawer since last fiscal closing.
        /// </summary>
        public decimal DrawerAmount { get; internal set; }
        /// <summary>
        /// Total amount of all payments since last payment totalizer reset.
        /// </summary>
        public decimal PaymentsInPeriod { get; internal set; }
    }
    #endregion

    #region Service progress info.
    /// <summary>
    /// Class representing a state of a service operation in progress.
    /// </summary>
    public class ServiceProgressInfo
    {
        /// <summary>
        /// Price look-up code of a progress state.
        /// </summary>
        public PLU CurrentProgressItem { get; }
        /// <summary>
        /// Represents a position of a progress state.
        /// </summary>
        public int CurrentProgressAmount { get; }
        /// <summary>
        /// Represents a number of progress states in a service operation or equals zero if number of states is undetermined.
        /// </summary>
        public int TotalProgressAmount { get; }
        /// <summary>
        /// Describes a service operation that created this instance.
        /// </summary>
        public ServiceProgressType ProgressType { get; }

        public ServiceProgressInfo(PLU item, int current, int total, ServiceProgressType type)
        {
            CurrentProgressItem = item;
            CurrentProgressAmount = current;
            TotalProgressAmount = total;
            ProgressType = type;
        }
    }
    /// <summary>
    /// Represents the type of service operation in progress concerning price look-up codes.
    /// </summary>
    public enum ServiceProgressType
    {
        /// <summary>
        /// Service is reading price look-up code information from a cash register.
        /// </summary>
        Reading,
        /// <summary>
        /// Service is writing price look-up code information to a cash register.
        /// </summary>
        Writing,
        /// <summary>
        /// Service is performing a sale on a cash register.
        /// </summary>
        Selling
    }
    #endregion

    #region Internal PLU extension methods.
    internal static class PLUExtensions
    {
        internal static void SetReadOnly(this IEnumerable<PLU> source, bool value)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            foreach (var plu in source)
            {
                if (plu != null) plu.IsReadOnly = value;
            }
        }
        internal static decimal GetTotal(this IEnumerable<PLUInfo> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            decimal total = 0.0M;
            foreach (var plu in source)
            {
                if (plu != null) total += plu.Price * plu.Quantity;
            }
            return total;
        }
    }
    #endregion
}