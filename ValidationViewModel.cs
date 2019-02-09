using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Linq.Expressions;

namespace Binding_Attribute_Validation.ViewModels
{
    public abstract class ValidationViewModel : ViewModel, IDataErrorInfo
    {
        #region Static Implementation

        /// <summary>
        /// Gets or sets the dictionary which maintains the cache for validators for each type.
        /// </summary>
        private static Dictionary<Type, ValidatorDescriptor[]> ValidatorDescriptorCache { get; set; }

        /// <summary>
        /// Gets an array of validators available for the given type.  If the validators for the type
        /// have not yet been cached, this method will gather and cache the validator information before
        /// returning.
        /// </summary>
        /// <param name="type">The type whose validator information should be returned.</param>
        /// <returns></returns>
        private static ValidatorDescriptor[] GetValidatorDescriptors(Type type)
        {
            if (ValidatorDescriptorCache.ContainsKey(type))
                return ValidatorDescriptorCache[type];

            List<ValidatorDescriptor> list = new List<ValidatorDescriptor>();
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var validationAttributes = (ValidationAttribute[])property.GetCustomAttributes(typeof(ValidationAttribute), true);
                foreach(var attr in validationAttributes)
                    list.Add(new ValidatorDescriptor(property, attr));
            }
            var array = list.ToArray();
            ValidatorDescriptorCache.Add(type, array);
            return array;
        }

        /// <summary>
        /// Validates a property for the given validation context.
        /// </summary>
        /// <param name="validationContext">The validation context to which the specified property belongs.</param>
        /// <param name="propertyName">The name of the property to validate.</param>
        /// <returns>An array of error messages returned from the validators on the property.</returns>
        private static string[] Validate(ValidationContext validationContext, string propertyName)
        {
            if (validationContext == null || validationContext.ObjectInstance == null)
                throw new ArgumentNullException("validationContext", "Validation context must be provided and the ObjectInstance must be set.");

            return GetValidatorDescriptors(validationContext.ObjectType)
                .Where(v => v.Property.Name == propertyName)
                .Where(v => v.ValidationAttribute.GetValidationResult(v.GetValue(validationContext.ObjectInstance), validationContext) != ValidationResult.Success)
                .Select(v => v.ValidationAttribute.ErrorMessage).ToArray();
        }

        static ValidationViewModel()
        {
            ValidatorDescriptorCache = new Dictionary<Type, ValidatorDescriptor[]>();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Maintains a list of explicitly set errors.
        /// </summary>
        private Dictionary<string, string> Errors { get; set; }
        private ValidationContext @ValidationContext { get; set; }
        protected bool UseExplicitValidation { get; set; }
        public bool HasErrors { get; private set; }

        #endregion

        #region Constructors

        protected ValidationViewModel()
        {
            this.UseExplicitValidation = false;
            this.Errors = new Dictionary<string, string>();
            this.ValidationContext = new ValidationContext(this, null, null);
        }

        #endregion

        #region Methods [public]

        /// <summary>
        /// Sets an error message for the property with the given name.  This method can only be used when UseExplicitValidation is true.
        /// </summary>
        /// <param name="propertyName">The name of the property in this ValidationViewModel instance to set the error for.</param>
        /// <param name="errorMessage">The error message to be set for the given property.</param>
        public void SetError(string propertyName, string errorMessage)
        {
            if (!this.UseExplicitValidation)
                throw new InvalidOperationException("The SetError method can only be used when UseExplicitValidation is true.");

            this.Errors[propertyName] = errorMessage;

            this.OnPropertyChanged(propertyName);
        }

        #endregion
        #region Methods [protected]

        /// <summary>
        /// Clears errors for the property with the given name.  This method can only be used when UseExplicitValidation is true.
        /// </summary>
        /// <param name="propertyName">The name of the property in this ValidationViewModel to remove the error for.</param>
        protected void ClearError(string propertyName)
        {
            if (!this.UseExplicitValidation)
                throw new InvalidOperationException("The ClearError method can only be used when UseExplicitValidation is true.");

            this.Errors.Remove(propertyName);
        }

        /// <summary>
        /// Clears all errors in this ValidationViewModel instance.  This method can only be used when UseExplicitValidation is true.
        /// </summary>
        protected void ClearAllErrors()
        {
            if (!this.UseExplicitValidation)
                throw new InvalidOperationException("The ClearAllErrors method can only be used when UseExplicitValidation is true.");

            var propertyNames = this.Errors.Keys.ToArray();

            this.Errors.Clear();

            foreach(var propertyName in propertyNames)
                this.OnPropertyChanged(propertyName);
        }

        public void Validate()
        {
            if (!this.UseExplicitValidation)
                throw new InvalidOperationException("The Validate method can only be used when UseExplicitValidation is true.");

            this.ClearAllErrors();

            foreach (var validator in GetValidatorDescriptors(this.GetType()))
            {
                var result = Validate(this.ValidationContext, validator.Property.Name);
                if (result.Length > 0)
                {
                    this.Errors[validator.Property.Name] = string.Join(Environment.NewLine, result);
                    this.OnPropertyChanged(validator.Property.Name);
                }
            }
        }

        #endregion

        #region IDataErrorInfo Members

        public string Error
        {
            get
            {
                if (this.UseExplicitValidation)
                {
                    this.HasErrors = !(this.Errors.Count == 0);
                    if (this.HasErrors)
                        return string.Join(Environment.NewLine, this.Errors.Values);
                    return null;
                }
                else
                {
                    var errors = GetValidatorDescriptors(this.GetType())
                        .Select(v => string.Join(Environment.NewLine, Validate(this.ValidationContext, v.Property.Name)))
                        .ToArray();

                    this.HasErrors = !(errors.Length == 0);
                    return (errors.Length == 0 ? null : string.Join(Environment.NewLine, errors));
                }
            }
        }

        public string this[string propertyName]
        {
            get
            {
                if (this.UseExplicitValidation)
                {
                    this.HasErrors = !(this.Errors.Count == 0);
                    if (this.Errors.ContainsKey(propertyName))
                        return this.Errors[propertyName];
                    return string.Empty;
                }
                else
                {
                    var validators = GetValidatorDescriptors(this.GetType()).Where(v => v.Property.Name == propertyName);
                    if (validators.Count() == 0)
                        return string.Empty;

                    var errors = validators
                        .Select(v => string.Join(Environment.NewLine, Validate(this.ValidationContext, v.Property.Name)))
                        .ToArray();

                    this.HasErrors = !(errors.Length == 0);
                    return (errors.Length == 0 ? string.Empty : string.Join(Environment.NewLine, errors));
                }
            }
        }

        #endregion

        #region Nested Classes

        private sealed class ValidatorDescriptor
        {
            public PropertyInfo Property { get; private set; }
            public ValidationAttribute ValidationAttribute { get; private set; }

            public ValidatorDescriptor(PropertyInfo property, ValidationAttribute validationAttribute)
            {
                this.Property = property;
                this.ValidationAttribute = validationAttribute;
            }

            public object GetValue(object instance)
            {
                return this.Property.GetValue(instance, null);
            }
        }

        #endregion
    }
}
