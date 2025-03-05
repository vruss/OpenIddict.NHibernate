/*
* Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
* See https://github.com/openiddict/openiddict-core for more information concerning
* the license and the contributors participating to this project.
*/

using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenIddict.NHibernate.Extensions;

internal class OpenIddictHelpers
{
	/// <summary>
	/// Finds the first base type that matches the specified generic type definition.
	/// </summary>
	/// <param name="type">The type to introspect.</param>
	/// <param name="definition">The generic type definition.</param>
	/// <returns>A <see cref="Type"/> instance if the base type was found, <see langword="null"/> otherwise.</returns>
	public static Type? FindGenericBaseType(Type type, Type definition)
	{
		return FindGenericBaseTypes(type, definition).FirstOrDefault();
	}

	/// <summary>
	/// Finds all the base types that matches the specified generic type definition.
	/// </summary>
	/// <param name="type">The type to introspect.</param>
	/// <param name="definition">The generic type definition.</param>
	/// <returns>A <see cref="Type"/> instance if the base type was found, <see langword="null"/> otherwise.</returns>
	public static IEnumerable<Type> FindGenericBaseTypes(Type type, Type definition)
	{
		ArgumentNullException.ThrowIfNull(type);

		ArgumentNullException.ThrowIfNull(definition);

		if (!definition.IsGenericTypeDefinition)
		{
			throw new ArgumentException("Argument is not a generic type", nameof(definition));
		}

		if (definition.IsInterface)
		{
			foreach (var contract in type.GetInterfaces())
			{
				if (!contract.IsGenericType && !contract.IsConstructedGenericType)
				{
					continue;
				}

				if (contract.GetGenericTypeDefinition() == definition)
				{
					yield return contract;
				}
			}
		}

		else
		{
			for (var candidate = type; candidate is not null; candidate = candidate.BaseType)
			{
				if (!candidate.IsGenericType && !candidate.IsConstructedGenericType)
				{
					continue;
				}

				if (candidate.GetGenericTypeDefinition() == definition)
				{
					yield return candidate;
				}
			}
		}
	}
}
