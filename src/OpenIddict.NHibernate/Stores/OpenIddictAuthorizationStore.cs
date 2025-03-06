using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Data;
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
using NHibernate.Event.Default;
using NHibernate.Linq;
using OpenIddict.Abstractions;
using OpenIddict.NHibernate.Extensions;
using OpenIddict.NHibernate.Models;

namespace OpenIddict.NHibernate.Stores
{
	/// <summary>
	/// Provides methods allowing to manage the authorizations stored in a database.
	/// </summary>
	public class OpenIddictAuthorizationStore : OpenIddictAuthorizationStore<OpenIddictAuthorization, OpenIddictApplication, OpenIddictToken, string>
	{
		public OpenIddictAuthorizationStore(IMemoryCache cache
			, IOpenIddictNHibernateContext context
			, IOptionsMonitor<OpenIddictNHibernateOptions> options
		)
			: base(cache, context, options)
		{
		}
	}

	/// <summary>
	/// Provides methods allowing to manage the authorizations stored in a database.
	/// </summary>
	/// <typeparam name="TKey">The type of the entity primary keys.</typeparam>
	public class OpenIddictAuthorizationStore<TKey> : OpenIddictAuthorizationStore<OpenIddictAuthorization<TKey>, OpenIddictApplication<TKey>, OpenIddictToken<TKey>, TKey>
		where TKey : IEquatable<TKey>
	{
		public OpenIddictAuthorizationStore(IMemoryCache cache
			, IOpenIddictNHibernateContext context
			, IOptionsMonitor<OpenIddictNHibernateOptions> options
		)
			: base(cache, context, options)
		{
		}
	}

	/// <summary>
	/// Provides methods allowing to manage the authorizations stored in a database.
	/// </summary>
	/// <typeparam name="TAuthorization">The type of the Authorization entity.</typeparam>
	/// <typeparam name="TApplication">The type of the Application entity.</typeparam>
	/// <typeparam name="TToken">The type of the Token entity.</typeparam>
	/// <typeparam name="TKey">The type of the entity primary keys.</typeparam>
	public class OpenIddictAuthorizationStore<TAuthorization, TApplication, TToken, TKey> : IOpenIddictAuthorizationStore<TAuthorization>
		where TAuthorization : OpenIddictAuthorization<TKey, TApplication, TToken>
		where TApplication : OpenIddictApplication<TKey, TAuthorization, TToken>
		where TToken : OpenIddictToken<TKey, TApplication, TAuthorization>
		where TKey : IEquatable<TKey>
	{
		public OpenIddictAuthorizationStore(IMemoryCache cache
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
		/// Determines the number of authorizations that exist in the database.
		/// </summary>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the number of authorizations in the database.
		/// </returns>
		public virtual async ValueTask<long> CountAsync(CancellationToken cancellationToken)
		{
			var session = await this.Context.GetSessionAsync(cancellationToken);
			return await session
				.Query<TAuthorization>()
				.LongCountAsync(cancellationToken);
		}

		/// <summary>
		/// Determines the number of authorizations that match the specified query.
		/// </summary>
		/// <typeparam name="TResult">The result type.</typeparam>
		/// <param name="query">The query to execute.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the number of authorizations that match the specified query.
		/// </returns>
		public virtual async ValueTask<long> CountAsync<TResult>(Func<IQueryable<TAuthorization>, IQueryable<TResult>> query, CancellationToken cancellationToken)
		{
			if (query == null)
			{
				throw new ArgumentNullException(nameof(query));
			}

			var session = await this.Context.GetSessionAsync(cancellationToken);
			return await query
				.Invoke(session.Query<TAuthorization>())
				.LongCountAsync(cancellationToken);
		}

		/// <summary>
		/// Creates a new authorization.
		/// </summary>
		/// <param name="authorization">The authorization to create.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual async ValueTask CreateAsync(TAuthorization authorization, CancellationToken cancellationToken)
		{
			if (authorization == null)
			{
				throw new ArgumentNullException(nameof(authorization));
			}

			var session = await this.Context.GetSessionAsync(cancellationToken);
			await session.SaveAsync(authorization, cancellationToken);
			await session.FlushAsync(cancellationToken);
		}

		/// <summary>
		/// Removes an existing authorization.
		/// </summary>
		/// <param name="authorization">The authorization to delete.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual async ValueTask DeleteAsync(TAuthorization authorization, CancellationToken cancellationToken)
		{
			if (authorization == null)
			{
				throw new ArgumentNullException(nameof(authorization));
			}

			var session = await this.Context.GetSessionAsync(cancellationToken);
			using var transaction = session.BeginTransaction(IsolationLevel.Serializable);

			try
			{
				// Delete all the tokens associated with the authorization.
				await session
					.Query<TToken>()
					.Where(token => token.Authorization.Id.Equals(authorization.Id))
					.DeleteAsync(cancellationToken);

				await session.DeleteAsync(authorization, cancellationToken);
				await transaction.CommitAsync(cancellationToken);
			}

			catch (StaleObjectStateException exception)
			{
				var message = new StringBuilder()
					.AppendLine("The authorization was concurrently updated and cannot be persisted in its current state.")
					.Append("Reload the authorization from the database and retry the operation.")
					.ToString();

				throw new OpenIddictExceptions.ConcurrencyException(message
					, exception
				);
			}
		}

		public async IAsyncEnumerable<TAuthorization> FindAsync(string? subject
			, string? client
			, string? status
			, string? type
			, ImmutableArray<string>? scopes
			, [EnumeratorCancellation] CancellationToken cancellationToken
		)
		{
			var session = await this.Context.GetSessionAsync(cancellationToken);
			var query = session.Query<TAuthorization>();

			if (!string.IsNullOrEmpty(subject))
			{
				query = query.Where(authorization => authorization.Subject == subject);
			}

			if (!string.IsNullOrEmpty(client))
			{
				var key = this.ConvertIdentifierFromString(client);

				query = query.Where(authorization => authorization.Application!.Id!.Equals(key));
			}

			if (!string.IsNullOrEmpty(status))
			{
				query = query.Where(authorization => authorization.Status == status);
			}

			if (!string.IsNullOrEmpty(type))
			{
				query = query.Where(authorization => authorization.Type == type);
			}

			query = query.Fetch(authorization => authorization.Application);

			await foreach (var authorization in query.AsAsyncEnumerable(cancellationToken))
			{
				var authorizationScopes = (await this.GetScopesAsync(authorization, cancellationToken))
					.ToHashSet(StringComparer.Ordinal);

				if (scopes is null || authorizationScopes.IsSupersetOf(scopes))
				{
					yield return authorization;
				}
			}
		}

		/// <summary>
		/// Retrieves the list of authorizations corresponding to the specified application identifier.
		/// </summary>
		/// <param name="identifier">The application identifier associated with the authorizations.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>The authorizations corresponding to the specified application.</returns>
		public virtual IAsyncEnumerable<TAuthorization> FindByApplicationIdAsync(string identifier
			, CancellationToken cancellationToken
		)
		{
			if (string.IsNullOrEmpty(identifier))
			{
				throw new ArgumentException("The identifier cannot be null or empty.", nameof(identifier));
			}

			return ExecuteAsync(cancellationToken);

			async IAsyncEnumerable<TAuthorization> ExecuteAsync([EnumeratorCancellation] CancellationToken ct)
			{
				var session = await this.Context.GetSessionAsync(ct);
				var key = this.ConvertIdentifierFromString(identifier);

				var authorizations = session
					.Query<TAuthorization>()
					.Fetch(authorization => authorization.Application)
					.Where(authorization => authorization.Application != null && authorization.Application.Id.Equals(key))
					.AsAsyncEnumerable(ct);

				await foreach (var authorization in authorizations)
				{
					yield return authorization;
				}
			}
		}

		/// <summary>
		/// Retrieves an authorization using its unique identifier.
		/// </summary>
		/// <param name="identifier">The unique identifier associated with the authorization.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the authorization corresponding to the identifier.
		/// </returns>
		public virtual async ValueTask<TAuthorization?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
		{
			if (string.IsNullOrEmpty(identifier))
			{
				throw new ArgumentException("The identifier cannot be null or empty.", nameof(identifier));
			}

			var session = await this.Context.GetSessionAsync(cancellationToken);

			// I hope this populates the lazy properties too
			var authorization = await session
				.GetAsync<TAuthorization>(this.ConvertIdentifierFromString(identifier), cancellationToken);

			return authorization;
		}

		/// <summary>
		/// Retrieves all the authorizations corresponding to the specified subject.
		/// </summary>
		/// <param name="subject">The subject associated with the authorization.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>The authorizations corresponding to the specified subject.</returns>
		public virtual IAsyncEnumerable<TAuthorization> FindBySubjectAsync(string subject
			, CancellationToken cancellationToken
		)
		{
			if (string.IsNullOrEmpty(subject))
			{
				throw new ArgumentException("The subject cannot be null or empty.", nameof(subject));
			}

			return ExecuteAsync(cancellationToken);

			async IAsyncEnumerable<TAuthorization> ExecuteAsync([EnumeratorCancellation] CancellationToken ct)
			{
				var session = await this.Context.GetSessionAsync(ct);
				var authorizations = session.Query<TAuthorization>()
					.Fetch(authorization => authorization.Application)
					.Where(authorization => authorization.Subject == subject)
					.AsAsyncEnumerable(ct);

				await foreach (var authorization in authorizations)
				{
					yield return authorization;
				}
			}
		}

		/// <summary>
		/// Retrieves the optional application identifier associated with an authorization.
		/// </summary>
		/// <param name="authorization">The authorization.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the application identifier associated with the authorization.
		/// </returns>
		public virtual ValueTask<string?> GetApplicationIdAsync(TAuthorization authorization, CancellationToken cancellationToken)
		{
			if (authorization == null)
			{
				throw new ArgumentNullException(nameof(authorization));
			}

			if (authorization.Application == null)
			{
				return new ValueTask<string?>(result: null);
			}

			return new ValueTask<string?>(this.ConvertIdentifierToString(authorization.Application.Id));
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
		public virtual async ValueTask<TResult?> GetAsync<TState, TResult>(Func<IQueryable<TAuthorization>, TState, IQueryable<TResult>> query
			, TState state
			, CancellationToken cancellationToken
		)
		{
			if (query == null)
			{
				throw new ArgumentNullException(nameof(query));
			}

			var session = await this.Context.GetSessionAsync(cancellationToken);

			var openIddictAuthorizations = session
				.Query<TAuthorization>()
				.Fetch(authorization => authorization.Application);

			return await query
				.Invoke(openIddictAuthorizations, state)
				.FirstOrDefaultAsync(cancellationToken);
		}

		public ValueTask<DateTimeOffset?> GetCreationDateAsync(TAuthorization authorization, CancellationToken cancellationToken)
		{
			if (authorization is null)
			{
				throw new ArgumentNullException(nameof(authorization));
			}

			if (authorization.CreationDate is null)
			{
				return new ValueTask<DateTimeOffset?>(result: null);
			}

			return new ValueTask<DateTimeOffset?>(DateTime.SpecifyKind(authorization.CreationDate.Value, DateTimeKind.Utc));
		}

		/// <summary>
		/// Retrieves the unique identifier associated with an authorization.
		/// </summary>
		/// <param name="authorization">The authorization.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the unique identifier associated with the authorization.
		/// </returns>
		public virtual ValueTask<string?> GetIdAsync(TAuthorization authorization, CancellationToken cancellationToken)
		{
			if (authorization == null)
			{
				throw new ArgumentNullException(nameof(authorization));
			}

			return new ValueTask<string?>(this.ConvertIdentifierToString(authorization.Id));
		}

		/// <summary>
		/// Retrieves the additional properties associated with an authorization.
		/// </summary>
		/// <param name="authorization">The authorization.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns all the additional properties associated with the authorization.
		/// </returns>
		public virtual ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(TAuthorization authorization, CancellationToken cancellationToken)
		{
			if (authorization is null)
			{
				throw new ArgumentNullException(nameof(authorization));
			}

			if (string.IsNullOrEmpty(authorization.Properties))
			{
				return new(ImmutableDictionary.Create<string, JsonElement>());
			}

			// Note: parsing the stringified properties is an expensive operation.
			// To mitigate that, the resulting object is stored in the memory cache.
			var key = string.Concat("046D0652-BF81-4669-976E-F424E2C9BCE9", "\x1e", authorization.Properties);
			var properties = this.Cache.GetOrCreate(key, entry =>
			{
				entry
					.SetPriority(CacheItemPriority.High)
					.SetSlidingExpiration(TimeSpan.FromMinutes(1));

				using var document = JsonDocument.Parse(authorization.Properties);
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
		/// Retrieves the scopes associated with an authorization.
		/// </summary>
		/// <param name="authorization">The authorization.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the scopes associated with the specified authorization.
		/// </returns>
		public virtual ValueTask<ImmutableArray<string>> GetScopesAsync(TAuthorization authorization, CancellationToken cancellationToken)
		{
			if (authorization is null)
			{
				throw new ArgumentNullException(nameof(authorization));
			}

			if (string.IsNullOrEmpty(authorization.Scopes))
			{
				return new ValueTask<ImmutableArray<string>>(ImmutableArray<string>.Empty);
			}

			// Note: parsing the stringified scopes is an expensive operation.
			// To mitigate that, the resulting array is stored in the memory cache.
			var key = string.Concat("C7938BAA-3236-4061-9016-623DF3471159", "\x1e", authorization.Scopes);
			var scopes = this.Cache.GetOrCreate(key, entry =>
			{
				entry
					.SetPriority(CacheItemPriority.High)
					.SetSlidingExpiration(TimeSpan.FromMinutes(1));

				using var document = JsonDocument.Parse(authorization.Scopes);
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

			return new ValueTask<ImmutableArray<string>>(scopes);
		}

		/// <summary>
		/// Retrieves the status associated with an authorization.
		/// </summary>
		/// <param name="authorization">The authorization.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the status associated with the specified authorization.
		/// </returns>
		public virtual ValueTask<string?> GetStatusAsync(TAuthorization authorization, CancellationToken cancellationToken)
		{
			if (authorization == null)
			{
				throw new ArgumentNullException(nameof(authorization));
			}

			return new ValueTask<string?>(authorization.Status);
		}

		/// <summary>
		/// Retrieves the subject associated with an authorization.
		/// </summary>
		/// <param name="authorization">The authorization.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the subject associated with the specified authorization.
		/// </returns>
		public virtual ValueTask<string?> GetSubjectAsync(TAuthorization authorization, CancellationToken cancellationToken)
		{
			if (authorization == null)
			{
				throw new ArgumentNullException(nameof(authorization));
			}

			return new ValueTask<string?>(authorization.Subject);
		}

		/// <summary>
		/// Retrieves the type associated with an authorization.
		/// </summary>
		/// <param name="authorization">The authorization.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the type associated with the specified authorization.
		/// </returns>
		public virtual ValueTask<string?> GetTypeAsync(TAuthorization authorization, CancellationToken cancellationToken)
		{
			if (authorization == null)
			{
				throw new ArgumentNullException(nameof(authorization));
			}

			return new ValueTask<string?>(authorization.Type);
		}

		/// <summary>
		/// Instantiates a new authorization.
		/// </summary>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the instantiated authorization, that can be persisted in the database.
		/// </returns>
		public virtual ValueTask<TAuthorization> InstantiateAsync(CancellationToken cancellationToken)
		{
			try
			{
				return new ValueTask<TAuthorization>(Activator.CreateInstance<TAuthorization>());
			}

			catch (MemberAccessException exception)
			{
				var message = new StringBuilder()
					.AppendLine("An error occurred while trying to create a new authorization instance.")
					.Append("Make sure that the authorization entity is not abstract and has a public parameterless constructor ")
					.Append("or create a custom authorization store that overrides 'InstantiateAsync()' to use a custom factory.")
					.ToString();

				return new ValueTask<TAuthorization>(Task.FromException<TAuthorization>(
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
		public virtual async IAsyncEnumerable<TAuthorization> ListAsync(int? count
			, int? offset
			, [EnumeratorCancellation] CancellationToken cancellationToken
		)
		{
			var session = await this.Context.GetSessionAsync(cancellationToken);

			var query = session
				.Query<TAuthorization>()
				.Fetch(authorization => authorization.Application)
				.OrderBy(authorization => authorization.Id)
				.AsQueryable();

			if (offset.HasValue)
			{
				query = query.Skip(offset.Value);
			}

			if (count.HasValue)
			{
				query = query.Take(count.Value);
			}

			await foreach (var authorization in query.AsAsyncEnumerable(cancellationToken))
			{
				yield return authorization;
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
		public virtual IAsyncEnumerable<TResult> ListAsync<TState, TResult>(Func<IQueryable<TAuthorization>, TState, IQueryable<TResult>> query
			, TState state
			, CancellationToken cancellationToken
		)
		{
			if (query == null)
			{
				throw new ArgumentNullException(nameof(query));
			}

			return ExecuteAsync(cancellationToken);

			async IAsyncEnumerable<TResult> ExecuteAsync([EnumeratorCancellation] CancellationToken ct)
			{
				var session = await this.Context.GetSessionAsync(ct);

				var openIddictAuthorizations = session
					.Query<TAuthorization>()
					.Fetch(authorization => authorization.Application);

				var elements = query
					.Invoke(openIddictAuthorizations, state)
					.AsAsyncEnumerable(ct);

				await foreach (var element in elements)
				{
					yield return element;
				}
			}
		}

		public async ValueTask<long> PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
		{
			var date = threshold.UtcDateTime;

			var session = await this.Context.GetSessionAsync(cancellationToken);
			using var transaction = session.BeginTransaction(IsolationLevel.RepeatableRead);

			try
			{
				// Delete all the tokens associated with the application.
				var deletedEntries = await session
					.Query<TAuthorization>()
					.Fetch(authorization => authorization.Tokens)
					.Where(authorization => authorization.CreationDate < date)
					.Where(authorization => authorization.Status != OpenIddictConstants.Statuses.Valid || authorization.Type == OpenIddictConstants.AuthorizationTypes.AdHoc)
					.Where(authorization => authorization.Tokens.Any())
					.DeleteAsync(cancellationToken);

				await session.FlushAsync(cancellationToken);
				await transaction.CommitAsync(cancellationToken);

				return deletedEntries;
			}
			catch (StaleObjectStateException exception)
			{
				throw new OpenIddictExceptions.ConcurrencyException("An error occurred while pruning authorizations."
					, exception
				);
			}
		}

		public async ValueTask<long> RevokeAsync(string? subject
			, string? client
			, string? status
			, string? type
			, CancellationToken cancellationToken
		)
		{
			var session = await this.Context.GetSessionAsync(cancellationToken);

			var query = session.Query<TAuthorization>();

			if (!string.IsNullOrEmpty(subject))
			{
				query = query.Where(authorization => authorization.Subject == subject);
			}

			if (!string.IsNullOrEmpty(client))
			{
				var key = this.ConvertIdentifierFromString(client);

				query = query.Where(authorization => authorization.Application!.Id!.Equals(key));
			}

			if (!string.IsNullOrEmpty(status))
			{
				query = query.Where(authorization => authorization.Status == status);
			}

			if (!string.IsNullOrEmpty(type))
			{
				query = query.Where(authorization => authorization.Type == type);
			}

			query = query.Fetch(authorization => authorization.Application);

			List<Exception>? exceptions = null;

			var result = 0L;

			foreach (var authorization in await query.ToListAsync(cancellationToken))
			{
				authorization.Status = OpenIddictConstants.Statuses.Revoked;

				try
				{
					await session.UpdateAsync(authorization, cancellationToken);
				}

				catch (Exception exception)
				{
					exceptions ??= [];
					exceptions.Add(exception);

					continue;
				}

				result++;
			}

			try
			{
				await session.FlushAsync(cancellationToken);
			}
			catch (Exception exception)
			{
				exceptions ??= [];
				exceptions.Add(exception);
			}

			if (exceptions is not null)
			{
				throw new AggregateException("An error occurred while pruning tokens.", exceptions);
			}

			return result;
		}

		public async ValueTask<long> RevokeByApplicationIdAsync(string identifier, CancellationToken cancellationToken)
		{
			ArgumentException.ThrowIfNullOrEmpty(identifier);

			var key = this.ConvertIdentifierFromString(identifier);

			List<Exception>? exceptions = null;

			var result = 0L;

			var session = await this.Context.GetSessionAsync(cancellationToken);

			var authorizations = await session
				.Query<TAuthorization>()
				.Fetch(authorization => authorization.Application)
				.Where(authorization => authorization.Application!.Id!.Equals(key))
				.ToListAsync(cancellationToken);

			foreach (var authorization in authorizations)
			{
				authorization.Status = OpenIddictConstants.Statuses.Revoked;

				try
				{
					await session.UpdateAsync(authorization, cancellationToken);
				}
				catch (Exception exception)
				{
					exceptions ??= [];
					exceptions.Add(exception);

					continue;
				}

				result++;
			}

			try
			{
				await session.FlushAsync(cancellationToken);
			}
			catch (Exception exception)
			{
				exceptions ??= [];
				exceptions.Add(exception);
			}

			if (exceptions is not null)
			{
				throw new AggregateException("An error occurred while pruning tokens.", exceptions);
			}

			return result;
		}

		public async ValueTask<long> RevokeBySubjectAsync(string subject, CancellationToken cancellationToken)
		{
			ArgumentException.ThrowIfNullOrEmpty(subject);

			List<Exception>? exceptions = null;

			var result = 0L;

			var session = await this.Context.GetSessionAsync(cancellationToken);

			var authorizations = await session
				.Query<TAuthorization>()
				.Fetch(authorization => authorization.Application)
				.Where(authorization => authorization.Subject == subject)
				.ToListAsync(cancellationToken);

			foreach (var authorization in authorizations)
			{
				authorization.Status = OpenIddictConstants.Statuses.Revoked;

				try
				{
					await session.UpdateAsync(authorization, cancellationToken);
				}
				catch (Exception exception)
				{
					exceptions ??= [];
					exceptions.Add(exception);

					continue;
				}

				result++;
			}

			try
			{
				await session.FlushAsync(cancellationToken);
			}
			catch (Exception exception)
			{
				exceptions ??= [];
				exceptions.Add(exception);
			}

			if (exceptions is not null)
			{
				throw new AggregateException("An error occurred while pruning tokens.", exceptions);
			}

			return result;
		}

		/// <summary>
		/// Sets the application identifier associated with an authorization.
		/// </summary>
		/// <param name="authorization">The authorization.</param>
		/// <param name="identifier">The unique identifier associated with the client application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual async ValueTask SetApplicationIdAsync(TAuthorization authorization, string? identifier, CancellationToken cancellationToken)
		{
			if (authorization == null)
			{
				throw new ArgumentNullException(nameof(authorization));
			}

			var session = await this.Context.GetSessionAsync(cancellationToken);

			if (!string.IsNullOrEmpty(identifier))
			{
				authorization.Application = await session.LoadAsync<TApplication>(this.ConvertIdentifierFromString(identifier), cancellationToken);
			}

			else
			{
				authorization.Application = null;
			}
		}

		public ValueTask SetCreationDateAsync(TAuthorization authorization, DateTimeOffset? date, CancellationToken cancellationToken)
		{
			if (authorization is null)
			{
				throw new ArgumentNullException(nameof(authorization));
			}

			authorization.CreationDate = date?.UtcDateTime;

			return default;
		}

		/// <summary>
		/// Sets the additional properties associated with an authorization.
		/// </summary>
		/// <param name="authorization">The authorization.</param>
		/// <param name="properties">The additional properties associated with the authorization.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetPropertiesAsync(TAuthorization authorization, ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
		{
			if (authorization is null)
			{
				throw new ArgumentNullException(nameof(authorization));
			}

			if (properties is not { Count: > 0 })
			{
				authorization.Properties = null;

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

			authorization.Properties = Encoding.UTF8.GetString(stream.ToArray());

			return default;
		}

		/// <summary>
		/// Sets the scopes associated with an authorization.
		/// </summary>
		/// <param name="authorization">The authorization.</param>
		/// <param name="scopes">The scopes associated with the authorization.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetScopesAsync(TAuthorization authorization, ImmutableArray<string> scopes, CancellationToken cancellationToken)
		{
			if (authorization is null)
			{
				throw new ArgumentNullException(nameof(authorization));
			}

			if (scopes.IsDefaultOrEmpty)
			{
				authorization.Scopes = null;

				return default;
			}

			using var stream = new MemoryStream();
			using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
			{
				Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
				Indented = false,
			});

			writer.WriteStartArray();

			foreach (var scope in scopes)
			{
				writer.WriteStringValue(scope);
			}

			writer.WriteEndArray();
			writer.Flush();

			authorization.Scopes = Encoding.UTF8.GetString(stream.ToArray());

			return default;
		}

		/// <summary>
		/// Sets the status associated with an authorization.
		/// </summary>
		/// <param name="authorization">The authorization.</param>
		/// <param name="status">The status associated with the authorization.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetStatusAsync(TAuthorization authorization, string? status, CancellationToken cancellationToken)
		{
			if (authorization == null)
			{
				throw new ArgumentNullException(nameof(authorization));
			}

			authorization.Status = status;

			return default;
		}

		/// <summary>
		/// Sets the subject associated with an authorization.
		/// </summary>
		/// <param name="authorization">The authorization.</param>
		/// <param name="subject">The subject associated with the authorization.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetSubjectAsync(TAuthorization authorization, string? subject, CancellationToken cancellationToken)
		{
			if (authorization == null)
			{
				throw new ArgumentNullException(nameof(authorization));
			}

			authorization.Subject = subject;

			return default;
		}

		/// <summary>
		/// Sets the type associated with an authorization.
		/// </summary>
		/// <param name="authorization">The authorization.</param>
		/// <param name="type">The type associated with the authorization.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetTypeAsync(TAuthorization authorization, string? type, CancellationToken cancellationToken)
		{
			if (authorization == null)
			{
				throw new ArgumentNullException(nameof(authorization));
			}

			authorization.Type = type;

			return default;
		}

		/// <summary>
		/// Updates an existing authorization.
		/// </summary>
		/// <param name="authorization">The authorization to update.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual async ValueTask UpdateAsync(TAuthorization authorization, CancellationToken cancellationToken)
		{
			if (authorization == null)
			{
				throw new ArgumentNullException(nameof(authorization));
			}

			var session = await this.Context.GetSessionAsync(cancellationToken);

			// Generate a new concurrency token and attach it
			// to the authorization before persisting the changes.
			authorization.ConcurrencyToken = Guid.NewGuid().ToString();

			try
			{
				await session.UpdateAsync(authorization, cancellationToken);
				await session.FlushAsync(cancellationToken);
			}

			catch (StaleObjectStateException exception)
			{
				var message = new StringBuilder()
					.AppendLine("The authorization was concurrently updated and cannot be persisted in its current state.")
					.Append("Reload the authorization from the database and retry the operation.")
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
		public virtual TKey? ConvertIdentifierFromString(string? identifier)
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
