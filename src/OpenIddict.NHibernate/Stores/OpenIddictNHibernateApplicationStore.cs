using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Data;
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
using Microsoft.IdentityModel.Tokens;
using NHibernate;
using NHibernate.Linq;
using OpenIddict.Abstractions;
using OpenIddict.NHibernate.Models;

namespace OpenIddict.NHibernate.Stores
{
	/// <summary>
	/// Provides methods allowing to manage the applications stored in a database.
	/// </summary>
	public class OpenIddictNHibernateApplicationStore : OpenIddictNHibernateApplicationStore<OpenIddictNHibernateApplication, OpenIddictNHibernateAuthorization, OpenIddictNHibernateToken, string>
	{
		public OpenIddictNHibernateApplicationStore(IMemoryCache cache
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
	public class OpenIddictNHibernateApplicationStore<TKey> : OpenIddictNHibernateApplicationStore<OpenIddictNHibernateApplication<TKey>, OpenIddictNHibernateAuthorization<TKey>, OpenIddictNHibernateToken<TKey>, TKey>
		where TKey : IEquatable<TKey>
	{
		public OpenIddictNHibernateApplicationStore(IMemoryCache cache
			, IOpenIddictNHibernateContext context
			, IOptionsMonitor<OpenIddictNHibernateOptions> options
		)
			: base(cache, context, options)
		{
		}
	}

	public class OpenIddictNHibernateApplicationStore<TApplication, TAuthorization, TToken, TKey> : OpenIddictNHibernateApplicationStore<TApplication, TAuthorization, TToken, TKey, TKey, TKey>
		where TApplication : OpenIddictNHibernateApplication<TKey, TAuthorization, TToken>
		where TAuthorization : OpenIddictNHibernateAuthorization<TKey, TApplication, TToken>
		where TToken : OpenIddictNHibernateToken<TKey, TApplication, TAuthorization>
		where TKey : IEquatable<TKey>
	{
		public OpenIddictNHibernateApplicationStore(IMemoryCache cache
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
	/// <typeparam name="TApplicationKey">The TApplication entity primary key.</typeparam>
	/// <typeparam name="TAuthorizationKey">The TAuthorization entity primary key.</typeparam>
	/// <typeparam name="TTokenKey">The TToken entity primary key.</typeparam>
	public class OpenIddictNHibernateApplicationStore<TApplication, TAuthorization, TToken, TApplicationKey, TAuthorizationKey, TTokenKey> : IOpenIddictApplicationStore<TApplication>
		where TApplication : OpenIddictNHibernateApplication<TApplicationKey, TAuthorization, TToken>
		where TAuthorization : OpenIddictNHibernateAuthorization<TAuthorizationKey, TApplication, TToken>
		where TToken : OpenIddictNHibernateToken<TTokenKey, TApplication, TAuthorization>
		where TApplicationKey : IEquatable<TApplicationKey>
		where TAuthorizationKey : IEquatable<TAuthorizationKey>
		where TTokenKey : IEquatable<TTokenKey>
	{
		public OpenIddictNHibernateApplicationStore(IMemoryCache cache
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
			ArgumentNullException.ThrowIfNull(query);

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
			ArgumentNullException.ThrowIfNull(application);

			var session = await this.Context.GetSessionAsync(cancellationToken);

			if (!session.Contains(application))
			{
				application = await session.MergeAsync(application, cancellationToken);
			}

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
			ArgumentNullException.ThrowIfNull(application);

			var session = await this.Context.GetSessionAsync(cancellationToken);
			using var transaction = session.BeginTransaction(IsolationLevel.Serializable);

			try
			{
				// Delete all the tokens associated with the application.
				await session
					.Query<TAuthorization>()
					.Fetch(authorization => authorization.Application)
					.Where(authorization => authorization.Application != null && authorization.Application.Id!.Equals(application.Id))
					.DeleteAsync(cancellationToken);

				// Delete all the tokens associated with the application.
				await session
					.Query<TToken>()
					.Fetch(token => token.Application)
					.Fetch(token => token.Authorization)
					.Where(token => token.Authorization == null) // Copied from https://github.com/openiddict/openiddict-core/blob/dev/src/OpenIddict.EntityFramework/Stores/OpenIddictEntityFrameworkApplicationStore.cs#L142-L142
					.Where(token => token.Application != null && token.Application.Id!.Equals(application.Id))
					.DeleteAsync(cancellationToken);

				await session.DeleteAsync(application, cancellationToken);
				await transaction.CommitAsync(cancellationToken);
			}

			catch (StaleObjectStateException exception)
			{
				var message = new StringBuilder()
					.AppendLine("The application was concurrently updated and cannot be persisted in its current state.")
					.Append("Reload the application from the database and retry the operation.")
					.ToString();

				throw new OpenIddictExceptions.ConcurrencyException(message
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
		public virtual async ValueTask<TApplication?> FindByClientIdAsync(string identifier, CancellationToken cancellationToken)
		{
			ArgumentException.ThrowIfNullOrEmpty(identifier);

			var session = await this.Context.GetSessionAsync(cancellationToken);

			return await session
				.Query<TApplication>()
				.Where(application => application.ClientId == identifier)
				.FirstOrDefaultAsync(cancellationToken);
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
		public virtual async ValueTask<TApplication?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
		{
			ArgumentException.ThrowIfNullOrEmpty(identifier);

			var session = await this.Context.GetSessionAsync(cancellationToken);

			return await session
				.GetAsync<TApplication>(this.ConvertIdentifierFromString<TApplicationKey>(identifier), cancellationToken);
		}

		/// <summary>
		/// Retrieves all the applications associated with the specified post_logout_redirect_uri.
		/// </summary>
		/// <param name="uri">The post_logout_redirect_uri associated with the applications.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>The client applications corresponding to the specified post_logout_redirect_uri.</returns>
		public virtual IAsyncEnumerable<TApplication> FindByPostLogoutRedirectUriAsync(string uri, CancellationToken cancellationToken)
		{
			ArgumentException.ThrowIfNullOrEmpty(uri);

			return ExecuteAsync(cancellationToken);

			async IAsyncEnumerable<TApplication> ExecuteAsync([EnumeratorCancellation] CancellationToken ct)
			{
				var session = await this.Context.GetSessionAsync(ct);

				// To optimize the efficiency of the query a bit, only applications whose stringified
				// PostLogoutRedirectUris contains the specified URL are returned. Once the applications
				// are retrieved, a second pass is made to ensure only valid elements are returned.
				// Implementers that use this method in a hot path may want to override this method
				// to use SQL Server 2016 functions like JSON_VALUE to make the query more efficient.
				var applications = session
					.Query<TApplication>()
					.Where(application => application.PostLogoutRedirectUris != null && application.PostLogoutRedirectUris.Contains(uri))
					.AsAsyncEnumerable(ct);

				await foreach (var application in applications)
				{
					var uris = await this.GetPostLogoutRedirectUrisAsync(application, cancellationToken);
					if (uris.Contains(uri, StringComparer.Ordinal))
					{
						yield return application;
					}
				}
			}
		}

		/// <summary>
		/// Retrieves all the applications associated with the specified redirect_uri.
		/// </summary>
		/// <param name="uri">The redirect_uri associated with the applications.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>The client applications corresponding to the specified redirect_uri.</returns>
		public virtual IAsyncEnumerable<TApplication> FindByRedirectUriAsync(string uri, CancellationToken cancellationToken)
		{
			ArgumentException.ThrowIfNullOrEmpty(uri);

			return ExecuteAsync(cancellationToken);

			async IAsyncEnumerable<TApplication> ExecuteAsync([EnumeratorCancellation] CancellationToken ct)
			{
				var session = await this.Context.GetSessionAsync(ct);

				// To optimize the efficiency of the query a bit, only applications whose stringified
				// RedirectUris contains the specified URL are returned. Once the applications
				// are retrieved, a second pass is made to ensure only valid elements are returned.
				// Implementers that use this method in a hot path may want to override this method
				// to use SQL Server 2016 functions like JSON_VALUE to make the query more efficient.
				var applications = session
					.Query<TApplication>()
					.Where(application => application.RedirectUris != null && application.RedirectUris.Contains(uri))
					.AsAsyncEnumerable(ct);

				await foreach (var application in applications)
				{
					var uris = await this.GetRedirectUrisAsync(application, cancellationToken);
					if (uris.Contains(uri, StringComparer.Ordinal))
					{
						yield return application;
					}
				}
			}
		}

		public ValueTask<string?> GetApplicationTypeAsync(TApplication application, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(application);

			return new ValueTask<string?>(application.ApplicationType);
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
			Func<IQueryable<TApplication>, TState, IQueryable<TResult>> query
			, TState state
			, CancellationToken cancellationToken
		)
		{
			ArgumentNullException.ThrowIfNull(query);

			var session = await this.Context.GetSessionAsync(cancellationToken);

			return await query
				.Invoke(session.Query<TApplication>(), state)
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
		public virtual ValueTask<string?> GetClientIdAsync(TApplication application, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(application);

			return new ValueTask<string?>(application.ClientId);
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
		public virtual ValueTask<string?> GetClientSecretAsync(TApplication application, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(application);

			return new ValueTask<string?>(application.ClientSecret);
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
		public virtual ValueTask<string?> GetClientTypeAsync(TApplication application, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(application);

			return new ValueTask<string?>(application.ClientType);
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
		public virtual ValueTask<string?> GetConsentTypeAsync(TApplication application, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(application);

			return new ValueTask<string?>(application.ConsentType);
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
		public virtual ValueTask<string?> GetDisplayNameAsync(TApplication application, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(application);

			return new ValueTask<string?>(application.DisplayName);
		}

		public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(TApplication application, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(application);

			if (string.IsNullOrEmpty(application.DisplayNames))
			{
				return new ValueTask<ImmutableDictionary<CultureInfo, string>>(ImmutableDictionary.Create<CultureInfo, string>());
			}

			// Note: parsing the stringified display names is an expensive operation.
			// To mitigate that, the resulting object is stored in the memory cache.
			var key = string.Concat("F417D612-9422-43B4-9BF8-9CE3D334987A", "\x1e", application.DisplayNames);
			var names = this.Cache.GetOrCreate(key, entry =>
			{
				entry
					.SetPriority(CacheItemPriority.High)
					.SetSlidingExpiration(TimeSpan.FromMinutes(1));

				using var document = JsonDocument.Parse(application.DisplayNames);
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
			})!;

			return new ValueTask<ImmutableDictionary<CultureInfo, string>>(names);
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
		public virtual ValueTask<string?> GetIdAsync(TApplication application, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			return new ValueTask<string?>(this.ConvertIdentifierToString(application.Id));
		}

		public ValueTask<JsonWebKeySet?> GetJsonWebKeySetAsync(TApplication application, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(application);

			if (string.IsNullOrEmpty(application.JsonWebKeySet))
			{
				return new ValueTask<JsonWebKeySet?>(result: null);
			}

			// Note: parsing the stringified JSON Web Key Set is an expensive operation.
			// To mitigate that, the resulting object is stored in the memory cache.
			var key = string.Concat("E119C0C7-105E-4B9E-9DD4-80FAD81A235D", "\x1e", application.JsonWebKeySet);
			var set = this.Cache.GetOrCreate(key, entry =>
			{
				entry
					.SetPriority(CacheItemPriority.High)
					.SetSlidingExpiration(TimeSpan.FromMinutes(1));

				return JsonWebKeySet.Create(application.JsonWebKeySet);
			})!;

			return new ValueTask<JsonWebKeySet?>(set);
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
			ArgumentNullException.ThrowIfNull(application);

			if (string.IsNullOrEmpty(application.Permissions))
			{
				return new ValueTask<ImmutableArray<string>>(ImmutableArray.Create<string>());
			}

			// Note: parsing the stringified permissions is an expensive operation.
			// To mitigate that, the resulting array is stored in the memory cache.
			var key = string.Concat("8DE1C53F-86C6-4E4E-B74E-5F4676B490EF", "\x1e", application.Permissions);
			var permissions = this.Cache.GetOrCreate(key, entry =>
			{
				entry
					.SetPriority(CacheItemPriority.High)
					.SetSlidingExpiration(TimeSpan.FromMinutes(1));

				using var document = JsonDocument.Parse(application.Permissions);
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
			});

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
			ArgumentNullException.ThrowIfNull(application);

			if (string.IsNullOrEmpty(application.PostLogoutRedirectUris))
			{
				return new ValueTask<ImmutableArray<string>>(ImmutableArray.Create<string>());
			}

			// Note: parsing the stringified addresses is an expensive operation.
			// To mitigate that, the resulting array is stored in the memory cache.
			var key = string.Concat("808ACFC7-1408-4749-AB71-24CD91E5D9AD", "\x1e", application.PostLogoutRedirectUris);
			var uris = this.Cache.GetOrCreate(key, entry =>
			{
				entry.SetPriority(CacheItemPriority.High)
					.SetSlidingExpiration(TimeSpan.FromMinutes(1));

				using var document = JsonDocument.Parse(application.PostLogoutRedirectUris);
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
			});

			return new ValueTask<ImmutableArray<string>>(uris);
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
			ArgumentNullException.ThrowIfNull(application);

			if (string.IsNullOrEmpty(application.Properties))
			{
				return new ValueTask<ImmutableDictionary<string, JsonElement>>(ImmutableDictionary.Create<string, JsonElement>());
			}

			// Note: parsing the stringified properties is an expensive operation.
			// To mitigate that, the resulting object is stored in the memory cache.
			var key = string.Concat("41AEFFF1-83E6-453D-8193-382B7ADA17A5", "\x1e", application.Properties);
			var properties = this.Cache.GetOrCreate(key, entry =>
			{
				entry.SetPriority(CacheItemPriority.High)
					.SetSlidingExpiration(TimeSpan.FromMinutes(1));

				using var document = JsonDocument.Parse(application.Properties);
				var builder = ImmutableDictionary.CreateBuilder<string, JsonElement>();

				foreach (var property in document.RootElement.EnumerateObject())
				{
					builder[property.Name] = property.Value.Clone();
				}

				return builder.ToImmutable();
			})!;

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
			ArgumentNullException.ThrowIfNull(application);

			if (string.IsNullOrEmpty(application.RedirectUris))
			{
				return new ValueTask<ImmutableArray<string>>(ImmutableArray.Create<string>());
			}

			// Note: parsing the stringified addresses is an expensive operation.
			// To mitigate that, the resulting array is stored in the memory cache.
			var key = string.Concat("F1E256D7-F737-4B92-89B7-6F6B674C8CB9", "\x1e", application.RedirectUris);
			var uris = this.Cache.GetOrCreate(key, entry =>
			{
				entry.SetPriority(CacheItemPriority.High)
					.SetSlidingExpiration(TimeSpan.FromMinutes(1));

				using var document = JsonDocument.Parse(application.RedirectUris);
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
			});

			return new ValueTask<ImmutableArray<string>>(uris);
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
			ArgumentNullException.ThrowIfNull(application);

			if (string.IsNullOrEmpty(application.Requirements))
			{
				return new ValueTask<ImmutableArray<string>>(ImmutableArray.Create<string>());
			}

			// Note: parsing the stringified requirements is an expensive operation.
			// To mitigate that, the resulting array is stored in the memory cache.
			var key = string.Concat("b4808a89-8969-4512-895f-a909c62a8995", "\x1e", application.Requirements);
			var requirements = this.Cache.GetOrCreate(key, entry =>
			{
				entry.SetPriority(CacheItemPriority.High)
					.SetSlidingExpiration(TimeSpan.FromMinutes(1));

				using var document = JsonDocument.Parse(application.Requirements);
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
			});

			return new ValueTask<ImmutableArray<string>>(requirements);
		}

		public ValueTask<ImmutableDictionary<string, string>> GetSettingsAsync(TApplication application, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(application);

			if (string.IsNullOrEmpty(application.Settings))
			{
				return new ValueTask<ImmutableDictionary<string, string>>(ImmutableDictionary.Create<string, string>());
			}

			// Note: parsing the stringified settings is an expensive operation.
			// To mitigate that, the resulting object is stored in the memory cache.
			var key = string.Concat("AE85C2DA-3532-44D7-81C6-BFE1893A64F2", "\x1e", application.Settings);
			var settings = this.Cache.GetOrCreate(key, entry =>
			{
				entry.SetPriority(CacheItemPriority.High)
					.SetSlidingExpiration(TimeSpan.FromMinutes(1));

				using var document = JsonDocument.Parse(application.Settings);
				var builder = ImmutableDictionary.CreateBuilder<string, string>();

				foreach (var property in document.RootElement.EnumerateObject())
				{
					var value = property.Value.GetString();
					if (string.IsNullOrEmpty(value))
					{
						continue;
					}

					builder[property.Name] = value;
				}

				return builder.ToImmutable();
			})!;

			return new ValueTask<ImmutableDictionary<string, string>>(settings);
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
				var message = new StringBuilder()
					.AppendLine("An error occurred while trying to create a new application instance.")
					.Append("Make sure that the application entity is not abstract and has a public parameterless constructor ")
					.Append("or create a custom application store that overrides 'InstantiateAsync()' to use a custom factory.")
					.ToString();

				return new ValueTask<TApplication>(Task.FromException<TApplication>(
						new InvalidOperationException(message
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
		public virtual async IAsyncEnumerable<TApplication> ListAsync(int? count
			, int? offset
			, [EnumeratorCancellation] CancellationToken cancellationToken
		)
		{
			var session = await this.Context.GetSessionAsync(cancellationToken);

			var query = session
				.Query<TApplication>()
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
		public virtual IAsyncEnumerable<TResult> ListAsync<TState, TResult>(Func<IQueryable<TApplication>, TState, IQueryable<TResult>> query
			, TState state
			, CancellationToken cancellationToken
		)
		{
			ArgumentNullException.ThrowIfNull(query);

			return ExecuteAsync(cancellationToken);

			async IAsyncEnumerable<TResult> ExecuteAsync([EnumeratorCancellation] CancellationToken ct)
			{
				var session = await this.Context.GetSessionAsync(ct);
				var elements = query.Invoke(session.Query<TApplication>(), state).AsAsyncEnumerable(ct);

				await foreach (var element in elements)
				{
					yield return element;
				}
			}
		}

		public ValueTask SetApplicationTypeAsync(TApplication application, string? applicationType, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(application);

			application.ApplicationType = applicationType;

			return default;
		}

		/// <summary>
		/// Sets the client identifier associated with an application.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="clientId">The client identifier associated with the application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetClientIdAsync(TApplication application, string? clientId, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(application);

			application.ClientId = clientId;

			return default;
		}

		/// <summary>
		/// Sets the client secret associated with an application.
		/// Note: depending on the manager used to create the application,
		/// the client secret may be hashed for security reasons.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="clientSecret">The client secret associated with the application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetClientSecretAsync(TApplication application, string? clientSecret, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(application);

			application.ClientSecret = clientSecret;

			return default;
		}

		/// <summary>
		/// Sets the client type associated with an application.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="clientType">The client type associated with the application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetClientTypeAsync(TApplication application, string? clientType, CancellationToken cancellationToken)
		{
			if (application == null)
			{
				throw new ArgumentNullException(nameof(application));
			}

			application.ClientType = clientType;

			return default;
		}

		/// <summary>
		/// Sets the consent type associated with an application.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="consentType">The consent type associated with the application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetConsentTypeAsync(TApplication application, string? consentType, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(application);

			application.ConsentType = consentType;

			return default;
		}

		/// <summary>
		/// Sets the display name associated with an application.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="displayName">The display name associated with the application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetDisplayNameAsync(TApplication application, string? displayName, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(application);

			application.DisplayName = displayName;

			return default;
		}

		public ValueTask SetDisplayNamesAsync(TApplication application, ImmutableDictionary<CultureInfo, string> displayNames, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(application);

			if (displayNames is not { Count: > 0 })
			{
				application.DisplayNames = null;

				return default;
			}

			using var stream = new MemoryStream();
			using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
			{
				Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
				Indented = false,
			});

			writer.WriteStartObject();

			foreach (var displayName in displayNames)
			{
				writer.WritePropertyName(displayName.Key.Name);
				writer.WriteStringValue(displayName.Value);
			}

			writer.WriteEndObject();
			writer.Flush();

			application.DisplayNames = Encoding.UTF8.GetString(stream.ToArray());

			return default;
		}

		public ValueTask SetJsonWebKeySetAsync(TApplication application, JsonWebKeySet? set, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(application);

			application.JsonWebKeySet = set is not null
				? JsonSerializer.Serialize(set)
				: null;

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
			ArgumentNullException.ThrowIfNull(application);

			if (permissions.IsDefaultOrEmpty)
			{
				application.Permissions = null;

				return default;
			}

			using var stream = new MemoryStream();
			using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
			{
				Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
				Indented = false,
			});

			writer.WriteStartArray();

			foreach (var permission in permissions)
			{
				writer.WriteStringValue(permission);
			}

			writer.WriteEndArray();
			writer.Flush();

			application.Permissions = Encoding.UTF8.GetString(stream.ToArray());

			return default;
		}

		/// <summary>
		/// Sets the logout callback addresses associated with an application.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="uris">The logout callback addresses associated with the application </param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetPostLogoutRedirectUrisAsync(TApplication application, ImmutableArray<string> uris, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(application);

			if (uris.IsDefaultOrEmpty)
			{
				application.PostLogoutRedirectUris = null;

				return default;
			}

			using var stream = new MemoryStream();
			using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
			{
				Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
				Indented = false,
			});

			writer.WriteStartArray();

			foreach (var uri in uris)
			{
				writer.WriteStringValue(uri);
			}

			writer.WriteEndArray();
			writer.Flush();

			application.PostLogoutRedirectUris = Encoding.UTF8.GetString(stream.ToArray());

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
			ArgumentNullException.ThrowIfNull(application);

			if (properties is not { Count: > 0 })
			{
				application.Properties = null;

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

			application.Properties = Encoding.UTF8.GetString(stream.ToArray());

			return default;
		}

		/// <summary>
		/// Sets the callback addresses associated with an application.
		/// </summary>
		/// <param name="application">The application.</param>
		/// <param name="uris">The callback addresses associated with the application </param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetRedirectUrisAsync(TApplication application, ImmutableArray<string> uris, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(application);

			if (uris.IsDefaultOrEmpty)
			{
				application.RedirectUris = null;

				return default;
			}

			using var stream = new MemoryStream();
			using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
			{
				Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
				Indented = false
			});

			writer.WriteStartArray();

			foreach (var uri in uris)
			{
				writer.WriteStringValue(uri);
			}

			writer.WriteEndArray();
			writer.Flush();

			application.RedirectUris = Encoding.UTF8.GetString(stream.ToArray());

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
			ArgumentNullException.ThrowIfNull(application);

			if (requirements.IsDefaultOrEmpty)
			{
				application.Requirements = null;

				return default;
			}

			using var stream = new MemoryStream();
			using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
			{
				Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
				Indented = false,
			});

			writer.WriteStartArray();

			foreach (var requirement in requirements)
			{
				writer.WriteStringValue(requirement);
			}

			writer.WriteEndArray();
			writer.Flush();

			application.Requirements = Encoding.UTF8.GetString(stream.ToArray());

			return default;
		}

		public ValueTask SetSettingsAsync(TApplication application, ImmutableDictionary<string, string> settings, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(application);

			if (settings is not { Count: > 0 })
			{
				application.Settings = null;

				return default;
			}

			using var stream = new MemoryStream();
			using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
			{
				Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
				Indented = false,
			});

			writer.WriteStartObject();

			foreach (var setting in settings)
			{
				writer.WritePropertyName(setting.Key);
				writer.WriteStringValue(setting.Value);
			}

			writer.WriteEndObject();
			writer.Flush();

			application.Settings = Encoding.UTF8.GetString(stream.ToArray());

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

			// Generate a new concurrency token and attach it
			// to the application before persisting the changes.
			application.ConcurrencyToken = Random.Shared.Next();

			try
			{
				await session.UpdateAsync(application, cancellationToken);
				await session.FlushAsync(cancellationToken);
			}

			catch (StaleObjectStateException exception)
			{
				var message = new StringBuilder()
					.AppendLine("The application was concurrently updated and cannot be persisted in its current state.")
					.Append("Reload the application from the database and retry the operation.")
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
		public virtual TKey? ConvertIdentifierFromString<TKey>(string? identifier)
			where TKey : IEquatable<TKey>
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
		public virtual string? ConvertIdentifierToString<TKey>(TKey? identifier)
			where TKey : IEquatable<TKey>
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
