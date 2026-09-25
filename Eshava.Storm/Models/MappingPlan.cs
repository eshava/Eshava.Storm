using System;
using System.Collections.Generic;
using System.Reflection;

namespace Eshava.Storm.Models
{
	/// <summary>
	/// How the columns of a result are mapped onto an object and its owned objects. Built once per reader
	/// and requested type, then applied to every row.
	/// </summary>
	internal class MappingPlan
	{
		public MappingPlan(Type type)
		{
			Type = type;
		}

		public Type Type { get; }

		/// <summary>
		/// The owned objects, parents before their children. Slot 0 is the object itself, owned object n is slot n + 1.
		/// </summary>
		public List<(int ParentSlot, PropertyInfo Property, Type Type)> OwnedObjects { get; } = new List<(int ParentSlot, PropertyInfo Property, Type Type)>();

		/// <summary>
		/// The values to read, ordered by column ordinal
		/// </summary>
		public List<(int Slot, PropertyInfo Property, int Ordinal)> Values { get; } = new List<(int Slot, PropertyInfo Property, int Ordinal)>();
	}
}