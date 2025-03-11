using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NHibernate;
using NHibernate.Linq;
using OpenIddict.Abstractions;
using OpenIddict.NHibernate.Models;

namespace OpenIddict.NHibernate.Stores
{
	/// <summary>
	/// Provides methods allowing to manage the scopes stored in a database.
	/// </summary>
	public class OpenIddictNHibernateScopeStore : OpenIddictNHibernateScopeStore<OpenIddictNHibernateScope, string>
	{
		public OpenIddictNHibernateScopeStore(IMemoryCache cache
			, IOpenIddictNHibernateContext context
			, IOptionsMonitor<OpenIddictNHibernateOptions> options
		)
			: base(cache, context, options)
		{
		}
	}

	/// <summary>
	/// Provides methods allowing to manage the scopes stored in a database.
	/// </summary>
	/// <typeparam name="TKey">The type of the entity primary keys.</typeparam>
	public class OpenIddictNHibernateScopeStore<TKey> : OpenIddictNHibernateScopeStore<OpenIddictNHibernateScope<TKey>, TKey>
		where TKey : IEquatable<TKey>
	{
		public OpenIddictNHibernateScopeStore(IMemoryCache cache
			, IOpenIddictNHibernateContext context
			, IOptionsMonitor<OpenIddictNHibernateOptions> options
		)
			: base(cache, context, options)
		{
		}
	}

	/// <summary>
	/// Provides methods allowing to manage the scopes stored in a database.
	/// </summary>
	/// <typeparam name="TScope">The type of the Scope entity.</typeparam>
	/// <typeparam name="TKey">The type of the entity primary keys.</typeparam>
	public class OpenIddictNHibernateScopeStore<TScope, TKey> : IOpenIddictScopeStore<TScope>
		where TScope : OpenIddictNHibernateScope<TKey>
		where TKey : IEquatable<TKey>
	{
		public OpenIddictNHibernateScopeStore(IMemoryCache cache
			, IOpenIddictNHibernateContext context
			, IOptionsMonitor<OpenIddictNHibernateOptions> options
		)
		{
			this.Cache = cache;
			this.Context = context;
			this.Options = options;
		}

		/// <summary>
		/// Gets the memory cache associated with the current store.
		/// </summary>
		protected IMemoryCache Cache { get; }

		/// <summary>
		/// Gets the database context associated with the current store.
		/// </summary>
		protected IOpenIddictNHibernateContext Context { get; }

		/// <summary>
		/// Gets the options associated with the current store.
		/// </summary>
		protected IOptionsMonitor<OpenIddictNHibernateOptions> Options { get; }

		/// <summary>
		/// Determines the number of scopes that exist in the database.
		/// </summary>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the number of scopes in the database.
		/// </returns>
		public virtual async ValueTask<long> CountAsync(CancellationToken cancellationToken)
		{
			var session = await this.Context.GetSessionAsync(cancellationToken);

			return await session
				.Query<TScope>()
				.LongCountAsync(cancellationToken);
		}

		/// <summary>
		/// Determines the number of scopes that match the specified query.
		/// </summary>
		/// <typeparam name="TResult">The result type.</typeparam>
		/// <param name="query">The query to execute.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the number of scopes that match the specified query.
		/// </returns>
		public virtual async ValueTask<long> CountAsync<TResult>(Func<IQueryable<TScope>, IQueryable<TResult>> query, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(nameof(query));

			var session = await this.Context.GetSessionAsync(cancellationToken);

			return await query
				.Invoke(session.Query<TScope>())
				.LongCountAsync(cancellationToken);
		}

		/// <summary>
		/// Creates a new scope.
		/// </summary>
		/// <param name="scope">The scope to create.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual async ValueTask CreateAsync(TScope scope, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(nameof(scope));

			var session = await this.Context.GetSessionAsync(cancellationToken);

			await session.SaveAsync(scope, cancellationToken);
			await session.FlushAsync(cancellationToken);
		}

		/// <summary>
		/// Removes an existing scope.
		/// </summary>
		/// <param name="scope">The scope to delete.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual async ValueTask DeleteAsync(TScope scope, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(nameof(scope));

			var session = await this.Context.GetSessionAsync(cancellationToken);

			try
			{
				await session.DeleteAsync(scope, cancellationToken);
				await session.FlushAsync(cancellationToken);
			}

			catch (StaleObjectStateException exception)
			{
				var message = new StringBuilder()
					.AppendLine("The scope was concurrently updated and cannot be persisted in its current state.")
					.Append("Reload the scope from the database and retry the operation.")
					.ToString();

				throw new OpenIddictExceptions.ConcurrencyException(message
					, exception
				);
			}
		}

		/// <summary>
		/// Retrieves a scope using its unique identifier.
		/// </summary>
		/// <param name="identifier">The unique identifier associated with the scope.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the scope corresponding to the identifier.
		/// </returns>
		public virtual async ValueTask<TScope?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
		{
			ArgumentException.ThrowIfNullOrEmpty(identifier);

			var session = await this.Context.GetSessionAsync(cancellationToken);

			return await session
				.GetAsync<TScope>(this.ConvertIdentifierFromString(identifier), cancellationToken);
		}

		/// <summary>
		/// Retrieves a scope using its name.
		/// </summary>
		/// <param name="name">The name associated with the scope.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the scope corresponding to the specified name.
		/// </returns>
		public virtual async ValueTask<TScope?> FindByNameAsync(string name, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(name);

			var session = await this.Context.GetSessionAsync(cancellationToken);

			return await session
				.Query<TScope>()
				.FirstOrDefaultAsync(scope => scope.Name == name, cancellationToken);
		}

		/// <summary>
		/// Retrieves a list of scopes using their name.
		/// </summary>
		/// <param name="names">The names associated with the scopes.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>The scopes corresponding to the specified names.</returns>
		public virtual IAsyncEnumerable<TScope> FindByNamesAsync(ImmutableArray<string> names, CancellationToken cancellationToken)
		{
			if (names.Any(string.IsNullOrEmpty))
			{
				throw new ArgumentException("Scope names cannot be null or empty.", nameof(names));
			}

			return ExecuteAsync(cancellationToken);

			async IAsyncEnumerable<TScope> ExecuteAsync([EnumeratorCancellation] CancellationToken ct)
			{
				var session = await this.Context.GetSessionAsync(ct);

				// Note: Enumerable.Contains() is deliberately used without the extension method syntax to ensure
				// ImmutableArray.Contains() (which is not fully supported by NHibernate) is not used instead.
				await foreach (var scope in session
					.Query<TScope>()
					.Where(scope => Enumerable.Contains(names, scope.Name))
					.AsAsyncEnumerable(ct))
				{
					yield return scope;
				}
			}
		}

		/// <summary>
		/// Retrieves all the scopes that contain the specified resource.
		/// </summary>
		/// <param name="resource">The resource associated with the scopes.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>The scopes associated with the specified resource.</returns>
		public virtual IAsyncEnumerable<TScope> FindByResourceAsync(string resource, CancellationToken cancellationToken)
		{
			ArgumentException.ThrowIfNullOrEmpty(resource);

			return ExecuteAsync(cancellationToken);

			async IAsyncEnumerable<TScope> ExecuteAsync([EnumeratorCancellation] CancellationToken ct)
			{
				var session = await this.Context.GetSessionAsync(ct);

				// To optimize the efficiency of the query a bit, only scopes whose stringified
				// Resources column contains the specified resource are returned. Once the scopes
				// are retrieved, a second pass is made to ensure only valid elements are returned.
				// Implementers that use this method in a hot path may want to override this method
				// to use SQL Server 2016 functions like JSON_VALUE to make the query more efficient.

				var scopes = session
					.Query<TScope>()
					.Where(scope => scope.Resources != null && scope.Resources.Contains(resource))
					.AsAsyncEnumerable(ct);

				await foreach (var scope in scopes)
				{
					var resources = await this.GetResourcesAsync(scope, cancellationToken);
					if (resources.Contains(resource, StringComparer.Ordinal))
					{
						yield return scope;
					}

					yield return scope;
				}
			}
		}

		/// <summary>
		/// Executes the specified query and returns the first element.
		/// </summary>
		/// <typeparam name="TState">The state type.</typeparam>
		/// <typeparam name="TResult">The result type.</typeparam>
		/// <param name="query">The query to execute.</param>
		/// <param name="state">The optional state.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the first element returned when executing the query.
		/// </returns>
		public virtual async ValueTask<TResult?> GetAsync<TState, TResult>(
			Func<IQueryable<TScope>, TState, IQueryable<TResult>> query
			, TState state
			, CancellationToken cancellationToken
		)
		{
			ArgumentNullException.ThrowIfNull(query);

			var session = await this.Context.GetSessionAsync(cancellationToken);

			return await query
				.Invoke(session.Query<TScope>(), state)
				.FirstOrDefaultAsync(cancellationToken);
		}

		/// <summary>
		/// Retrieves the description associated with a scope.
		/// </summary>
		/// <param name="scope">The scope.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the description associated with the specified scope.
		/// </returns>
		public virtual ValueTask<string?> GetDescriptionAsync(TScope scope, CancellationToken cancellationToken)
		{
			if (scope == null)
			{
				throw new ArgumentNullException(nameof(scope));
			}

			return new ValueTask<string?>(scope.Description);
		}

		public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDescriptionsAsync(TScope scope, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(scope);

			if (string.IsNullOrEmpty(scope.Descriptions))
			{
				return new ValueTask<ImmutableDictionary<CultureInfo, string>>(ImmutableDictionary.Create<CultureInfo, string>());
			}

			// Note: parsing the stringified descriptions is an expensive operation.
			// To mitigate that, the resulting object is stored in the memory cache.
			var key = string.Concat("CD72F66F-E36C-454A-AEA6-DADBC25C61A9", "\x1e", scope.Descriptions);
			var descriptions = this.Cache.GetOrCreate(key
				, entry =>
				{
					entry
						.SetPriority(CacheItemPriority.High)
						.SetSlidingExpiration(TimeSpan.FromMinutes(1));

					using var document = JsonDocument.Parse(scope.Descriptions);
					var builder = ImmutableDictionary.CreateBuilder<CultureInfo, string>();

					foreach (var property in document.RootElement.EnumerateObject())
					{
						var value = property.Value.GetString();
						if (string.IsNullOrEmpty(value))
						{
							continue;
						}

						builder[CultureInfo.GetCultureInfo(property.Name)] = value;
					}

					return builder.ToImmutable();
				}
			)!;

			return new ValueTask<ImmutableDictionary<CultureInfo, string>>(descriptions);
		}

		/// <summary>
		/// Retrieves the display name associated with a scope.
		/// </summary>
		/// <param name="scope">The scope.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the display name associated with the scope.
		/// </returns>
		public virtual ValueTask<string?> GetDisplayNameAsync(TScope scope, CancellationToken cancellationToken)
		{
			if (scope == null)
			{
				throw new ArgumentNullException(nameof(scope));
			}

			return new ValueTask<string?>(scope.DisplayName);
		}

		public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(TScope scope, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(scope);

			if (string.IsNullOrEmpty(scope.DisplayNames))
			{
				return new ValueTask<ImmutableDictionary<CultureInfo, string>>(ImmutableDictionary.Create<CultureInfo, string>());
			}

			// Note: parsing the stringified display names is an expensive operation.
			// To mitigate that, the resulting object is stored in the memory cache.
			var key = string.Concat("400DB81F-933E-4A06-806B-B2023AB61B3A", "\x1e", scope.DisplayNames);
			var names = this.Cache.GetOrCreate(key
				, entry =>
				{
					entry.SetPriority(CacheItemPriority.High)
						.SetSlidingExpiration(TimeSpan.FromMinutes(1));

					using var document = JsonDocument.Parse(scope.DisplayNames);
					var builder = ImmutableDictionary.CreateBuilder<CultureInfo, string>();

					foreach (var property in document.RootElement.EnumerateObject())
					{
						var value = property.Value.GetString();
						if (string.IsNullOrEmpty(value))
						{
							continue;
						}

						builder[CultureInfo.GetCultureInfo(property.Name)] = value;
					}

					return builder.ToImmutable();
				}
			)!;

			return new ValueTask<ImmutableDictionary<CultureInfo, string>>(names);
		}

		/// <summary>
		/// Retrieves the unique identifier associated with a scope.
		/// </summary>
		/// <param name="scope">The scope.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the unique identifier associated with the scope.
		/// </returns>
		public virtual ValueTask<string?> GetIdAsync(TScope scope, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(scope);

			return new ValueTask<string?>(this.ConvertIdentifierToString(scope.Id));
		}

		/// <summary>
		/// Retrieves the name associated with a scope.
		/// </summary>
		/// <param name="scope">The scope.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the name associated with the specified scope.
		/// </returns>
		public virtual ValueTask<string?> GetNameAsync(TScope scope, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(scope);

			return new ValueTask<string?>(scope.Name);
		}

		/// <summary>
		/// Retrieves the additional properties associated with a scope.
		/// </summary>
		/// <param name="scope">The scope.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns all the additional properties associated with the scope.
		/// </returns>
		public virtual ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(TScope scope, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(scope);

			if (string.IsNullOrEmpty(scope.Properties))
			{
				return new ValueTask<ImmutableDictionary<string, JsonElement>>(ImmutableDictionary.Create<string, JsonElement>());
			}

			// Note: parsing the stringified properties is an expensive operation.
			// To mitigate that, the resulting object is stored in the memory cache.
			var key = string.Concat("B281DF4C-C641-4E0B-89F7-6A07AFACE307", "\x1e", scope.Properties);
			var properties = this.Cache.GetOrCreate(key
				, entry =>
				{
					entry.SetPriority(CacheItemPriority.High)
						.SetSlidingExpiration(TimeSpan.FromMinutes(1));

					using var document = JsonDocument.Parse(scope.Properties);
					var builder = ImmutableDictionary.CreateBuilder<string, JsonElement>();

					foreach (var property in document.RootElement.EnumerateObject())
					{
						builder[property.Name] = property.Value.Clone();
					}

					return builder.ToImmutable();
				}
			)!;

			return new ValueTask<ImmutableDictionary<string, JsonElement>>(properties);
		}

		/// <summary>
		/// Retrieves the resources associated with a scope.
		/// </summary>
		/// <param name="scope">The scope.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns all the resources associated with the scope.
		/// </returns>
		public virtual ValueTask<ImmutableArray<string>> GetResourcesAsync(TScope scope, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(scope);

			if (string.IsNullOrEmpty(scope.Resources))
			{
				return new ValueTask<ImmutableArray<string>>(ImmutableArray.Create<string>());
			}

			// Note: parsing the stringified resources is an expensive operation.
			// To mitigate that, the resulting array is stored in the memory cache.
			var key = string.Concat("F0C5DEF7-3917-48E1-A5DE-4BA240A76F1B", "\x1e", scope.Resources);
			var resources = this.Cache.GetOrCreate(key
				, entry =>
				{
					entry.SetPriority(CacheItemPriority.High)
						.SetSlidingExpiration(TimeSpan.FromMinutes(1));

					using var document = JsonDocument.Parse(scope.Resources);
					var builder = ImmutableArray.CreateBuilder<string>(document.RootElement.GetArrayLength());

					foreach (var element in document.RootElement.EnumerateArray())
					{
						var value = element.GetString();
						if (string.IsNullOrEmpty(value))
						{
							continue;
						}

						builder.Add(value);
					}

					return builder.ToImmutable();
				}
			);

			return new ValueTask<ImmutableArray<string>>(resources);
		}

		/// <summary>
		/// Instantiates a new scope.
		/// </summary>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the instantiated scope, that can be persisted in the database.
		/// </returns>
		public virtual ValueTask<TScope> InstantiateAsync(CancellationToken cancellationToken)
		{
			try
			{
				return new ValueTask<TScope>(Activator.CreateInstance<TScope>());
			}

			catch (MemberAccessException exception)
			{
				var stringBuilder = new StringBuilder();
				stringBuilder.AppendLine("An error occurred while trying to create a new scope instance.");
				stringBuilder.Append("Make sure that the scope entity is not abstract and has a public parameterless constructor ");
				stringBuilder.Append("or create a custom scope store that overrides 'InstantiateAsync()' to use a custom factory.");

				return new ValueTask<TScope>(Task.FromException<TScope>(
						new InvalidOperationException(stringBuilder.ToString()
							, exception
						)
					)
				);
			}
		}

		/// <summary>
		/// Executes the specified query and returns all the corresponding elements.
		/// </summary>
		/// <param name="count">The number of results to return.</param>
		/// <param name="offset">The number of results to skip.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>All the elements returned when executing the specified query.</returns>
		public virtual async IAsyncEnumerable<TScope> ListAsync(int? count
			, int? offset
			, [EnumeratorCancellation] CancellationToken cancellationToken
		)
		{
			var session = await this.Context.GetSessionAsync(cancellationToken);

			var query = session.Query<TScope>()
				.OrderBy(scope => scope.Id)
				.AsQueryable();

			if (offset.HasValue)
			{
				query = query.Skip(offset.Value);
			}

			if (count.HasValue)
			{
				query = query.Take(count.Value);
			}

			await foreach (var scope in query.AsAsyncEnumerable(cancellationToken))
			{
				yield return scope;
			}
		}

		/// <summary>
		/// Executes the specified query and returns all the corresponding elements.
		/// </summary>
		/// <typeparam name="TState">The state type.</typeparam>
		/// <typeparam name="TResult">The result type.</typeparam>
		/// <param name="query">The query to execute.</param>
		/// <param name="state">The optional state.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>All the elements returned when executing the specified query.</returns>
		public virtual IAsyncEnumerable<TResult> ListAsync<TState, TResult>(Func<IQueryable<TScope>, TState, IQueryable<TResult>> query
			, TState state
			, CancellationToken cancellationToken
		)
		{
			ArgumentNullException.ThrowIfNull(query);

			return ExecuteAsync(cancellationToken);

			async IAsyncEnumerable<TResult> ExecuteAsync([EnumeratorCancellation] CancellationToken ct)
			{
				var session = await this.Context.GetSessionAsync(ct);

				await foreach (var element in query(session.Query<TScope>(), state)
					.AsAsyncEnumerable(ct))
				{
					yield return element;
				}
			}
		}

		/// <summary>
		/// Sets the description associated with a scope.
		/// </summary>
		/// <param name="scope">The scope.</param>
		/// <param name="description">The description associated with the authorization.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetDescriptionAsync(TScope scope, string? description, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(scope);

			scope.Description = description;

			return default;
		}

		public ValueTask SetDescriptionsAsync(TScope scope, ImmutableDictionary<CultureInfo, string> descriptions, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(scope);

			if (descriptions is not { Count: > 0 })
			{
				scope.Descriptions = null;

				return default;
			}

			using var stream = new MemoryStream();
			using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
			{
				Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
				Indented = false,
			});

			writer.WriteStartObject();

			foreach (var description in descriptions)
			{
				writer.WritePropertyName(description.Key.Name);
				writer.WriteStringValue(description.Value);
			}

			writer.WriteEndObject();
			writer.Flush();

			scope.Descriptions = Encoding.UTF8.GetString(stream.ToArray());

			return default;
		}

		/// <summary>
		/// Sets the display name associated with a scope.
		/// </summary>
		/// <param name="scope">The scope.</param>
		/// <param name="name">The display name associated with the scope.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetDisplayNameAsync(TScope scope, string? name, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(scope);

			scope.DisplayName = name;

			return default;
		}

		public ValueTask SetDisplayNamesAsync(TScope scope, ImmutableDictionary<CultureInfo, string> names, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(scope);

			if (names is not { Count: > 0 })
			{
				scope.DisplayNames = null;

				return default;
			}

			using var stream = new MemoryStream();
			using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
			{
				Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
				Indented = false,
			});

			writer.WriteStartObject();

			foreach (var name in names)
			{
				writer.WritePropertyName(name.Key.Name);
				writer.WriteStringValue(name.Value);
			}

			writer.WriteEndObject();
			writer.Flush();

			scope.DisplayNames = Encoding.UTF8.GetString(stream.ToArray());

			return default;
		}

		/// <summary>
		/// Sets the name associated with a scope.
		/// </summary>
		/// <param name="scope">The scope.</param>
		/// <param name="name">The name associated with the authorization.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetNameAsync(TScope scope, string? name, CancellationToken cancellationToken)
		{
			if (scope == null)
			{
				throw new ArgumentNullException(nameof(scope));
			}

			scope.Name = name;

			return default;
		}

		/// <summary>
		/// Sets the additional properties associated with a scope.
		/// </summary>
		/// <param name="scope">The scope.</param>
		/// <param name="properties">The additional properties associated with the scope.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetPropertiesAsync(TScope scope, ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(scope);

			if (properties is not { Count: > 0 })
			{
				scope.Properties = null;

				return default;
			}

			using var stream = new MemoryStream();
			using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
			{
				Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
				Indented = false,
			});

			writer.WriteStartObject();

			foreach (var property in properties)
			{
				writer.WritePropertyName(property.Key);
				property.Value.WriteTo(writer);
			}

			writer.WriteEndObject();
			writer.Flush();

			scope.Properties = Encoding.UTF8.GetString(stream.ToArray());

			return default;
		}

		/// <summary>
		/// Sets the resources associated with a scope.
		/// </summary>
		/// <param name="scope">The scope.</param>
		/// <param name="resources">The resources associated with the scope.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetResourcesAsync(TScope scope, ImmutableArray<string> resources, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(scope);

			if (resources.IsDefaultOrEmpty)
			{
				scope.Resources = null;

				return default;
			}

			using var stream = new MemoryStream();
			using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
			{
				Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
				Indented = false,
			});

			writer.WriteStartArray();

			foreach (var resource in resources)
			{
				writer.WriteStringValue(resource);
			}

			writer.WriteEndArray();
			writer.Flush();

			scope.Resources = Encoding.UTF8.GetString(stream.ToArray());

			return default;
		}

		/// <summary>
		/// Updates an existing scope.
		/// </summary>
		/// <param name="scope">The scope to update.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual async ValueTask UpdateAsync(TScope scope, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(scope);

			var session = await this.Context.GetSessionAsync(cancellationToken);

			// Generate a new concurrency token and attach it
			// to the scope before persisting the changes.
			scope.ConcurrencyToken = Random.Shared.Next();

			try
			{
				await session.UpdateAsync(scope, cancellationToken);
				await session.FlushAsync(cancellationToken);
			}

			catch (StaleObjectStateException exception)
			{
				var message = new StringBuilder()
					.AppendLine("The scope was concurrently updated and cannot be persisted in its current state.")
					.Append("Reload the scope from the database and retry the operation.")
					.ToString();

				throw new OpenIddictExceptions.ConcurrencyException(message
					, exception
				);
			}
		}

		/// <summary>
		/// Converts the provided identifier to a strongly typed key object.
		/// </summary>
		/// <param name="identifier">The identifier to convert.</param>
		/// <returns>An instance of <typeparamref name="TKey"/> representing the provided identifier.</returns>
		public virtual TKey? ConvertIdentifierFromString(string identifier)
		{
			if (string.IsNullOrEmpty(identifier))
			{
				return default;
			}

			return (TKey?)TypeDescriptor
				.GetConverter(typeof(TKey))
				.ConvertFromInvariantString(identifier);
		}

		/// <summary>
		/// Converts the provided identifier to its string representation.
		/// </summary>
		/// <param name="identifier">The identifier to convert.</param>
		/// <returns>A <see cref="string"/> representation of the provided identifier.</returns>
		public virtual string? ConvertIdentifierToString(TKey? identifier)
		{
			if (Equals(identifier, default(TKey)))
			{
				return null;
			}

			return TypeDescriptor
				.GetConverter(typeof(TKey))
				.ConvertToInvariantString(identifier);
		}
	}
}
