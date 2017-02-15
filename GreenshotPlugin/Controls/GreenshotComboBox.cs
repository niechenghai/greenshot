﻿#region Dapplo 2017 - GNU Lesser General Public License

// Dapplo - building blocks for .NET applications
// Copyright (C) 2017 Dapplo
// 
// For more information see: http://dapplo.net/
// Dapplo repositories are hosted on GitHub: https://github.com/dapplo
// 
// This file is part of Greenshot
// 
// Greenshot is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Greenshot is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
// 
// You should have a copy of the GNU Lesser General Public License
// along with Greenshot. If not, see <http://www.gnu.org/licenses/lgpl.txt>.

#endregion

#region Usings

using System;
using System.ComponentModel;
using System.Windows.Forms;
using GreenshotPlugin.Core;

#endregion

namespace GreenshotPlugin.Controls
{
	public class GreenshotComboBox : ComboBox, IGreenshotConfigBindable
	{
		private Type _enumType;
		private Enum _selectedEnum;

		public GreenshotComboBox()
		{
			SelectedIndexChanged += delegate { StoreSelectedEnum(); };
		}

		[Category("Greenshot")]
		[DefaultValue("Core")]
		[Description("Specifies the Ini-Section to map this control with.")]
		public string SectionName { get; set; } = "Core";

		[Category("Greenshot")]
		[DefaultValue(null)]
		[Description("Specifies the property name to map the configuration.")]
		public string PropertyName { get; set; }

		public void SetValue(Enum currentValue)
		{
			if (currentValue != null)
			{
				_selectedEnum = currentValue;
				SelectedItem = Language.Translate(currentValue);
			}
		}

		/// <summary>
		///     This is a method to popululate the ComboBox
		///     with the items from the enumeration
		/// </summary>
		/// <param name="enumType">TEnum to populate with</param>
		public void Populate(Type enumType)
		{
			// Store the enum-type, so we can work with it
			_enumType = enumType;

			var availableValues = Enum.GetValues(enumType);
			Items.Clear();
			foreach (var enumValue in availableValues)
			{
				Items.Add(Language.Translate((Enum) enumValue));
			}
		}

		/// <summary>
		///     Store the selected value internally
		/// </summary>
		private void StoreSelectedEnum()
		{
			var enumTypeName = _enumType.Name;
			var selectedValue = SelectedItem as string;
			var availableValues = Enum.GetValues(_enumType);
			object returnValue = null;

			try
			{
				returnValue = Enum.Parse(_enumType, selectedValue);
			}
			catch (Exception)
			{
				// Ignore
			}

			foreach (Enum enumValue in availableValues)
			{
				var enumKey = enumTypeName + "." + enumValue;
				if (Language.HasKey(enumKey))
				{
					var translation = Language.GetString(enumTypeName + "." + enumValue);
					if (translation.Equals(selectedValue))
					{
						returnValue = enumValue;
					}
				}
			}
			_selectedEnum = (Enum) returnValue;
		}

		/// <summary>
		///     Get the selected enum value from the combobox, uses generics
		/// </summary>
		/// <returns>The enum value of the combobox</returns>
		public Enum GetSelectedEnum()
		{
			return _selectedEnum;
		}
	}
}