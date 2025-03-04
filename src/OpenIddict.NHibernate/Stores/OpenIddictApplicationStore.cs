using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NHibernate;
using NHibernate.Linq;
using OpenIddict.Abstractions;
using OpenIddict.NHibernate.Models;

namespace OpenIddict.NHibernate.Stores
{
	/// <summary>
	/// Provides methods allowing to manage the applications stored in a database.
	/// </summary>
	public class OpenIddictApplicationStore : OpenIddictApplicationStore<OpenIddictApplication, OpenIddictAuthorization, OpenIddictToken, string>
	{
		public OpenIddictApplicationStore(IMemoryCache cache
			, IOpenIddictNHibernateContext context
			, IOptionsMonitor<OpenIddictNHibernateOptions> options
		)
			: base(cache, context, options)
		{
		}
	}

	/// <summary>
	/// Provides methods allowing to manage the applications stored in a database.
	/// </summary>
	/// <typeparam name="TKey">The type of the entity primary keys.</typeparam>
	public class OpenIddictApplicationStore<TKey> : OpenIddictApplicationStore<OpenIddictApplication<TKey>, OpenIddictAuthorization<TKey>, OpenIddictToken<TKey>, TKey>
		where TKey : IEquatable<TKey>
	{
		public OpenIddictApplicationStore(
			IMemoryCache cache
			, IOpenIddictNHibernateContext context
			, IOptionsMonitor<OpenIddictNHibernateOptions> options
		)
			: base(cache, context, options)
		{
		}
	}

	/// <summary>
	/// Provides methods allowing to manage the applications stored in a database.
	/// </summary>
	/// <typeparam name="TApplication">The type of the Application entity.</typeparam>
	/// <typeparam name="TAuthorization">The type of the Authorization entity.</typeparam>
	/// <typeparam name="TToken">The type of the Token entity.</typeparam>
	/// <typeparam name="TKey">The type of the entity primary keys.</typeparam>
	public class OpenIddictApplicationStore<TApplication, TAuthorization, TToken, TKey> : IOpenIddictApplicationStore<TApplication>
		where TApplication : OpenIddictApplication<TKey, TAuthorization, TToken>
		where TAuthorization : OpenIddictAuthorization<TKey, TApplication, TToken>
		where TToken : OpenIddictToken<TKey, TApplication, TAuthorization>
		where TKey : IEquatable<TKey>
	{
		public OpenIddictApplicationStore(
			IMemoryCache cache
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
		/// Determines the number of applications that exist in the database.
		/// </summary>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the number of applications in the database.
		/// </returns>
		public virtual async ValueTask<long> CountAsync(CancellationToken cancellationToken)
		{
			var session = await this.Context.GetSessionAsync(cancellationToken);

			return await session
				.Query<TApplication>()
				.LongCountAsync(cancellationToken);
		}

		/// <summary>
		/// Determines the number of applications that match the specified query.
		/// </summary>
		/// <typeparam name="TResult">The result type.</typeparam>
		/// <param name="query">The query to execute.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the number of applications that match the specified query.
		/// </returns>
		public virtual async ValueTask<long> CountAsync<TResult>(Func<IQueryable<TApplication>, IQueryable<TResult>> query, CancellationToken cancellationToken)
		{
			if (query == null)
			{
				throw new ArgumentNullException(nameof(query));
			}

			var session = await this.Context.GetSessionAsync(cancellationToken);

			return await query
				.Invoke(session.Query<TApplication>())
				.LongCountAsync(cancellationToken);
		}

		/// <summary>
		/// Creates a new application.
		/// </summary>
		/// <param name="application">The application to create.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual async ValueTask CreateAsync(TApplication application, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			var session = await this.Context.GetSessionAsync(cancellationToken);
			await session.PersistAsync(application, cancellationToken);
			await session.FlushAsync(cancellationToken);
		}

		/// <summary>
		/// Removes an existing application.
		/// </summary>
		/// <param name="application">The application to delete.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual async ValueTask DeleteAsync(TApplication application, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			var session = await this.Context.GetSessionAsync(cancellationToken);

			try
			{
				// Delete all the tokens associated with the application.
				await (from authorization in session.Query<TAuthorization>()
					where authorization.Application.Id.Equals(application.Id)
					select authorization).DeleteAsync(cancellationToken);

				// Delete all the tokens associated with the application.
				await (from token in session.Query<TToken>()
					where token.Application.Id.Equals(application.Id)
					select token).DeleteAsync(cancellationToken);

				await session.DeleteAsync(application, cancellationToken);
				await session.FlushAsync(cancellationToken);
			}

			catch (StaleObjectStateException exception)
			{
				throw new OpenIddictExceptions.ConcurrencyException(new StringBuilder()
						.AppendLine("The application was concurrently updated and cannot be persisted in its current state.")
						.Append("Reload the application from the database and retry the operation.")
						.ToString()
					, exception
				);
			}
		}

		/// <summary>
		/// Retrieves an application using its client identifier.
		/// </summary>
		/// <param name="identifier">The client identifier associated with the application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the client application corresponding to the identifier.
		/// </returns>
		public virtual async ValueTask<TApplication> FindByClientIdAsync(string identifier, CancellationToken cancellationToken)
		{
			if (string.IsNullOrEmpty(identifier))
			{
				throw new ArgumentException("The identifier cannot be null or empty.", nameof(identifier));
			}

			var session = await this.Context.GetSessionAsync(cancellationToken);

			return await (from application in session.Query<TApplication>()
				where application.ClientId == identifier
				select application).FirstOrDefaultAsync(cancellationToken);
		}

		/// <summary>
		/// Retrieves an application using its unique identifier.
		/// </summary>
		/// <param name="identifier">The unique identifier associated with the application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the client application corresponding to the identifier.
		/// </returns>
		public virtual async ValueTask<TApplication> FindByIdAsync(string identifier, CancellationToken cancellationToken)
		{
			if (string.IsNullOrEmpty(identifier))
			{
				throw new ArgumentException("The identifier cannot be null or empty.", nameof(identifier));
			}

			var session = await this.Context.GetSessionAsync(cancellationToken);
			return await session.GetAsync<TApplication>(this.ConvertIdentifierFromString(identifier), cancellationToken);
		}

		/// <summary>
		/// Retrieves all the applications associated with the specified post_logout_redirect_uri.
		/// </summary>
		/// <param name="address">The post_logout_redirect_uri associated with the applications.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>The client applications corresponding to the specified post_logout_redirect_uri.</returns>
		public virtual IAsyncEnumerable<TApplication> FindByPostLogoutRedirectUriAsync(string address, CancellationToken cancellationToken)
		{
			if (string.IsNullOrEmpty(address))
			{
				throw new ArgumentException("The address cannot be null or empty.", nameof(address));
			}

			return ExecuteAsync(cancellationToken);

			async IAsyncEnumerable<TApplication> ExecuteAsync(CancellationToken cancellationToken)
			{
				var session = await this.Context.GetSessionAsync(cancellationToken);

				// To optimize the efficiency of the query a bit, only applications whose stringified
				// PostLogoutRedirectUris contains the specified URL are returned. Once the applications
				// are retrieved, a second pass is made to ensure only valid elements are returned.
				// Implementers that use this method in a hot path may want to override this method
				// to use SQL Server 2016 functions like JSON_VALUE to make the query more efficient.
				await foreach (var application in session.Query<TApplication>()
					.Where(application => application.PostLogoutRedirectUris.Contains(address))
					.AsAsyncEnumerable(cancellationToken)
					.WhereAwait(async application => (await this.GetPostLogoutRedirectUrisAsync(application, cancellationToken))
						.Contains(address, StringComparer.Ordinal)
					))
				{
					yield return application;
				}
			}
		}

		/// <summary>
		/// Retrieves all the applications associated with the specified redirect_uri.
		/// </summary>
		/// <param name="address">The redirect_uri associated with the applications.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>The client applications corresponding to the specified redirect_uri.</returns>
		public virtual IAsyncEnumerable<TApplication> FindByRedirectUriAsync(string address, CancellationToken cancellationToken)
		{
			if (string.IsNullOrEmpty(address))
			{
				throw new ArgumentException("The address cannot be null or empty.", nameof(address));
			}

			return ExecuteAsync(cancellationToken);

			async IAsyncEnumerable<TApplication> ExecuteAsync(CancellationToken cancellationToken)
			{
				var session = await this.Context.GetSessionAsync(cancellationToken);

				// To optimize the efficiency of the query a bit, only applications whose stringified
				// RedirectUris contains the specified URL are returned. Once the applications
				// are retrieved, a second pass is made to ensure only valid elements are returned.
				// Implementers that use this method in a hot path may want to override this method
				// to use SQL Server 2016 functions like JSON_VALUE to make the query more efficient.
				await foreach (var application in session.Query<TApplication>()
					.Where(application => application.RedirectUris.Contains(address))
					.AsAsyncEnumerable(cancellationToken)
					.WhereAwait(async application => (await this.GetRedirectUrisAsync(application, cancellationToken))
						.Contains(address, StringComparer.Ordinal)
					))
				{
					yield return application;
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
		public virtual async ValueTask<TResult> GetAsync<TState, TResult>(
			Func<IQueryable<TApplication>, TState, IQueryable<TResult>> query
			, TState state
			, CancellationToken cancellationToken
		)
		{
			if (query == null)
			{
				throw new ArgumentNullException(nameof(query));
			}

			var session = await this.Context.GetSessionAsync(cancellationToken);
			return await query(session.Query<TApplication>(), state)
				.FirstOrDefaultAsync(cancellationToken);
		}

		/// <summary>
		/// Retrieves the client identifier associated with an application.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the client identifier associated with the application.
		/// </returns>
		public virtual ValueTask<string> GetClientIdAsync(TApplication application, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			return new ValueTask<string>(application.ClientId);
		}

		/// <summary>
		/// Retrieves the client secret associated with an application.
		/// Note: depending on the manager used to create the application,
		/// the client secret may be hashed for security reasons.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the client secret associated with the application.
		/// </returns>
		public virtual ValueTask<string> GetClientSecretAsync(TApplication application, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			return new ValueTask<string>(application.ClientSecret);
		}

		/// <summary>
		/// Retrieves the client type associated with an application.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the client type of the application (by default, "public").
		/// </returns>
		public virtual ValueTask<string> GetClientTypeAsync(TApplication application, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			return new ValueTask<string>(application.Type);
		}

		/// <summary>
		/// Retrieves the consent type associated with an application.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the consent type of the application (by default, "explicit").
		/// </returns>
		public virtual ValueTask<string> GetConsentTypeAsync(TApplication application, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			return new ValueTask<string>(application.ConsentType);
		}

		/// <summary>
		/// Retrieves the display name associated with an application.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the display name associated with the application.
		/// </returns>
		public virtual ValueTask<string> GetDisplayNameAsync(TApplication application, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			return new ValueTask<string>(application.DisplayName);
		}

		/// <summary>
		/// Retrieves the unique identifier associated with an application.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the unique identifier associated with the application.
		/// </returns>
		public virtual ValueTask<string> GetIdAsync(TApplication application, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			return new ValueTask<string>(this.ConvertIdentifierToString(application.Id));
		}

		/// <summary>
		/// Retrieves the permissions associated with an application.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns all the permissions associated with the application.
		/// </returns>
		public virtual ValueTask<ImmutableArray<string>> GetPermissionsAsync(TApplication application, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			if (string.IsNullOrEmpty(application.Permissions))
			{
				return new ValueTask<ImmutableArray<string>>(ImmutableArray.Create<string>());
			}

			// Note: parsing the stringified permissions is an expensive operation.
			// To mitigate that, the resulting array is stored in the memory cache.
			var key = string.Concat("0347e0aa-3a26-410a-97e8-a83bdeb21a1f", "\x1e", application.Permissions);
			var permissions = this.Cache.GetOrCreate(key
				, entry =>
				{
					entry.SetPriority(CacheItemPriority.High)
						.SetSlidingExpiration(TimeSpan.FromMinutes(1));

					return JsonSerializer.Deserialize<ImmutableArray<string>>(application.Permissions);
				}
			);

			return new ValueTask<ImmutableArray<string>>(permissions);
		}

		/// <summary>
		/// Retrieves the logout callback addresses associated with an application.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns all the post_logout_redirect_uri associated with the application.
		/// </returns>
		public virtual ValueTask<ImmutableArray<string>> GetPostLogoutRedirectUrisAsync(TApplication application, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			if (string.IsNullOrEmpty(application.PostLogoutRedirectUris))
			{
				return new ValueTask<ImmutableArray<string>>(ImmutableArray.Create<string>());
			}

			// Note: parsing the stringified addresses is an expensive operation.
			// To mitigate that, the resulting array is stored in the memory cache.
			var key = string.Concat("fb14dfb9-9216-4b77-bfa9-7e85f8201ff4", "\x1e", application.PostLogoutRedirectUris);
			var addresses = this.Cache.GetOrCreate(key
				, entry =>
				{
					entry.SetPriority(CacheItemPriority.High)
						.SetSlidingExpiration(TimeSpan.FromMinutes(1));

					return JsonSerializer.Deserialize<ImmutableArray<string>>(application.PostLogoutRedirectUris);
				}
			);

			return new ValueTask<ImmutableArray<string>>(addresses);
		}

		/// <summary>
		/// Retrieves the additional properties associated with an application.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns all the additional properties associated with the application.
		/// </returns>
		public virtual ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(TApplication application, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			if (string.IsNullOrEmpty(application.Properties))
			{
				return new ValueTask<ImmutableDictionary<string, JsonElement>>(ImmutableDictionary.Create<string, JsonElement>());
			}

			// Note: parsing the stringified properties is an expensive operation.
			// To mitigate that, the resulting object is stored in the memory cache.
			var key = string.Concat("2e3e9680-5654-48d8-a27d-b8bb4f0f1d50", "\x1e", application.Properties);
			var properties = this.Cache.GetOrCreate(key
				, entry =>
				{
					entry.SetPriority(CacheItemPriority.High)
						.SetSlidingExpiration(TimeSpan.FromMinutes(1));

					return JsonSerializer.Deserialize<ImmutableDictionary<string, JsonElement>>(application.Properties);
				}
			);

			return new ValueTask<ImmutableDictionary<string, JsonElement>>(properties);
		}

		/// <summary>
		/// Retrieves the callback addresses associated with an application.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns all the redirect_uri associated with the application.
		/// </returns>
		public virtual ValueTask<ImmutableArray<string>> GetRedirectUrisAsync(TApplication application, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			if (string.IsNullOrEmpty(application.RedirectUris))
			{
				return new ValueTask<ImmutableArray<string>>(ImmutableArray.Create<string>());
			}

			// Note: parsing the stringified addresses is an expensive operation.
			// To mitigate that, the resulting array is stored in the memory cache.
			var key = string.Concat("851d6f08-2ee0-4452-bbe5-ab864611ecaa", "\x1e", application.RedirectUris);
			var addresses = this.Cache.GetOrCreate(key
				, entry =>
				{
					entry.SetPriority(CacheItemPriority.High)
						.SetSlidingExpiration(TimeSpan.FromMinutes(1));

					return JsonSerializer.Deserialize<ImmutableArray<string>>(application.RedirectUris);
				}
			);

			return new ValueTask<ImmutableArray<string>>(addresses);
		}

		/// <summary>
		/// Retrieves the requirements associated with an application.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns all the requirements associated with the application.
		/// </returns>
		public virtual ValueTask<ImmutableArray<string>> GetRequirementsAsync(TApplication application, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			if (string.IsNullOrEmpty(application.Requirements))
			{
				return new ValueTask<ImmutableArray<string>>(ImmutableArray.Create<string>());
			}

			// Note: parsing the stringified requirements is an expensive operation.
			// To mitigate that, the resulting array is stored in the memory cache.
			var key = string.Concat("b4808a89-8969-4512-895f-a909c62a8995", "\x1e", application.Requirements);
			var requirements = this.Cache.GetOrCreate(key
				, entry =>
				{
					entry.SetPriority(CacheItemPriority.High)
						.SetSlidingExpiration(TimeSpan.FromMinutes(1));

					return JsonSerializer.Deserialize<ImmutableArray<string>>(application.Requirements);
				}
			);

			return new ValueTask<ImmutableArray<string>>(requirements);
		}

		/// <summary>
		/// Instantiates a new application.
		/// </summary>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the instantiated application, that can be persisted in the database.
		/// </returns>
		public virtual ValueTask<TApplication> InstantiateAsync(CancellationToken cancellationToken)
		{
			try
			{
				return new ValueTask<TApplication>(Activator.CreateInstance<TApplication>());
			}

			catch (MemberAccessException exception)
			{
				return new ValueTask<TApplication>(Task.FromException<TApplication>(
						new InvalidOperationException(new StringBuilder()
								.AppendLine("An error occurred while trying to create a new application instance.")
								.Append("Make sure that the application entity is not abstract and has a public parameterless constructor ")
								.Append("or create a custom application store that overrides 'InstantiateAsync()' to use a custom factory.")
								.ToString()
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
		public virtual async IAsyncEnumerable<TApplication> ListAsync(
			int? count
			, int? offset
			, [EnumeratorCancellation] CancellationToken cancellationToken
		)
		{
			var session = await this.Context.GetSessionAsync(cancellationToken);
			var query = session.Query<TApplication>()
				.OrderBy(application => application.Id)
				.AsQueryable();

			if (offset.HasValue)
			{
				query = query.Skip(offset.Value);
			}

			if (count.HasValue)
			{
				query = query.Take(count.Value);
			}

			await foreach (var application in query.AsAsyncEnumerable(cancellationToken))
			{
				yield return application;
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
		public virtual IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
			Func<IQueryable<TApplication>, TState, IQueryable<TResult>> query
			, TState state
			, CancellationToken cancellationToken
		)
		{
			if (query == null)
			{
				throw new ArgumentNullException(nameof(query));
			}

			return ExecuteAsync(cancellationToken);

			async IAsyncEnumerable<TResult> ExecuteAsync(CancellationToken cancellationToken)
			{
				var session = await this.Context.GetSessionAsync(cancellationToken);

				await foreach (var element in query(session.Query<TApplication>(), state)
					.AsAsyncEnumerable(cancellationToken))
				{
					yield return element;
				}
			}
		}

		/// <summary>
		/// Sets the client identifier associated with an application.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="identifier">The client identifier associated with the application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetClientIdAsync(TApplication application, string identifier, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			application.ClientId = identifier;

			return default;
		}

		/// <summary>
		/// Sets the client secret associated with an application.
		/// Note: depending on the manager used to create the application,
		/// the client secret may be hashed for security reasons.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="secret">The client secret associated with the application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetClientSecretAsync(TApplication application, string secret, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			application.ClientSecret = secret;

			return default;
		}

		/// <summary>
		/// Sets the client type associated with an application.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="type">The client type associated with the application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetClientTypeAsync(TApplication application, string type, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			application.Type = type;

			return default;
		}

		/// <summary>
		/// Sets the consent type associated with an application.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="type">The consent type associated with the application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetConsentTypeAsync(TApplication application, string type, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			application.ConsentType = type;

			return default;
		}

		/// <summary>
		/// Sets the display name associated with an application.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="name">The display name associated with the application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetDisplayNameAsync(TApplication application, string name, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			application.DisplayName = name;

			return default;
		}

		/// <summary>
		/// Sets the permissions associated with an application.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="permissions">The permissions associated with the application </param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetPermissionsAsync(TApplication application, ImmutableArray<string> permissions, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			if (permissions.IsDefaultOrEmpty)
			{
				application.Permissions = null;

				return default;
			}

			application.Permissions = JsonSerializer.Serialize(permissions
				, new JsonSerializerOptions
				{
					Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
					, WriteIndented = false
				}
			);

			return default;
		}

		/// <summary>
		/// Sets the logout callback addresses associated with an application.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="addresses">The logout callback addresses associated with the application </param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetPostLogoutRedirectUrisAsync(TApplication application, ImmutableArray<string> addresses, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			if (addresses.IsDefaultOrEmpty)
			{
				application.PostLogoutRedirectUris = null;

				return default;
			}

			application.PostLogoutRedirectUris = JsonSerializer.Serialize(addresses
				, new JsonSerializerOptions
				{
					Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
					, WriteIndented = false
				}
			);

			return default;
		}

		/// <summary>
		/// Sets the additional properties associated with an application.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="properties">The additional properties associated with the application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetPropertiesAsync(TApplication application, ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			if (properties == null || properties.IsEmpty)
			{
				application.Properties = null;

				return default;
			}

			application.Properties = JsonSerializer.Serialize(properties
				, new JsonSerializerOptions
				{
					Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
					, WriteIndented = false
				}
			);

			return default;
		}

		/// <summary>
		/// Sets the callback addresses associated with an application.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="addresses">The callback addresses associated with the application </param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetRedirectUrisAsync(TApplication application, ImmutableArray<string> addresses, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			if (addresses.IsDefaultOrEmpty)
			{
				application.RedirectUris = null;

				return default;
			}

			application.RedirectUris = JsonSerializer.Serialize(addresses
				, new JsonSerializerOptions
				{
					Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
					, WriteIndented = false
				}
			);

			return default;
		}

		/// <summary>
		/// Sets the requirements associated with an application.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="requirements">The requirements associated with the application </param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetRequirementsAsync(TApplication application, ImmutableArray<string> requirements, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			if (requirements.IsDefaultOrEmpty)
			{
				application.Requirements = null;

				return default;
			}

			application.Requirements = JsonSerializer.Serialize(requirements
				, new JsonSerializerOptions
				{
					Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
					, WriteIndented = false
				}
			);

			return default;
		}

		/// <summary>
		/// Updates an existing application.
		/// </summary>
		/// <param name="application">The application to update.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual async ValueTask UpdateAsync(TApplication application, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			var session = await this.Context.GetSessionAsync(cancellationToken);

			try
			{
				await session.UpdateAsync(application, cancellationToken);
				await session.FlushAsync(cancellationToken);
			}

			catch (StaleObjectStateException exception)
			{
				throw new OpenIddictExceptions.ConcurrencyException(new StringBuilder()
						.AppendLine("The application was concurrently updated and cannot be persisted in its current state.")
						.Append("Reload the application from the database and retry the operation.")
						.ToString()
					, exception
				);
			}
		}

		/// <summary>
		/// Converts the provided identifier to a strongly typed key object.
		/// </summary>
		/// <param name="identifier">The identifier to convert.</param>
		/// <returns>An instance of <typeparamref name="TKey"/> representing the provided identifier.</returns>
		public virtual TKey ConvertIdentifierFromString(string identifier)
		{
			if (string.IsNullOrEmpty(identifier))
			{
				return default;
			}

			return (TKey)TypeDescriptor.GetConverter(typeof(TKey))
				.ConvertFromInvariantString(identifier);
		}

		/// <summary>
		/// Converts the provided identifier to its string representation.
		/// </summary>
		/// <param name="identifier">The identifier to convert.</param>
		/// <returns>A <see cref="string"/> representation of the provided identifier.</returns>
		public virtual string ConvertIdentifierToString(TKey identifier)
		{
			if (Equals(identifier, default(TKey)))
			{
				return null;
			}

			return TypeDescriptor.GetConverter(typeof(TKey))
				.ConvertToInvariantString(identifier);
		}
	}
}
