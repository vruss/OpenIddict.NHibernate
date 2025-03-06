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
using NHibernate.Linq;
using OpenIddict.Abstractions;
using OpenIddict.NHibernate.Models;

namespace OpenIddict.NHibernate.Stores
{
	/// <summary>
	/// Provides methods allowing to manage the tokens stored in a database.
	/// </summary>
	public class OpenIddictTokenStore : OpenIddictTokenStore<OpenIddictToken, OpenIddictApplication, OpenIddictAuthorization, string>
	{
		public OpenIddictTokenStore(IMemoryCache cache
			, IOpenIddictNHibernateContext context
			, IOptionsMonitor<OpenIddictNHibernateOptions> options
		)
			: base(cache, context, options)
		{
		}
	}

	/// <summary>
	/// Provides methods allowing to manage the tokens stored in a database.
	/// </summary>
	/// <typeparam name="TKey">The type of the entity primary keys.</typeparam>
	public class OpenIddictTokenStore<TKey> : OpenIddictTokenStore<OpenIddictToken<TKey>, OpenIddictApplication<TKey>, OpenIddictAuthorization<TKey>, TKey>
		where TKey : IEquatable<TKey>
	{
		public OpenIddictTokenStore(IMemoryCache cache
			, IOpenIddictNHibernateContext context
			, IOptionsMonitor<OpenIddictNHibernateOptions> options
		)
			: base(cache, context, options)
		{
		}
	}

	/// <summary>
	/// Provides methods allowing to manage the tokens stored in a database.
	/// </summary>
	/// <typeparam name="TToken">The type of the Token entity.</typeparam>
	/// <typeparam name="TApplication">The type of the Application entity.</typeparam>
	/// <typeparam name="TAuthorization">The type of the Authorization entity.</typeparam>
	/// <typeparam name="TKey">The type of the entity primary keys.</typeparam>
	public class OpenIddictTokenStore<TToken, TApplication, TAuthorization, TKey> : IOpenIddictTokenStore<TToken>
		where TToken : OpenIddictToken<TKey, TApplication, TAuthorization>
		where TApplication : OpenIddictApplication<TKey, TAuthorization, TToken>
		where TAuthorization : OpenIddictAuthorization<TKey, TApplication, TToken>
		where TKey : IEquatable<TKey>
	{
		public OpenIddictTokenStore(IMemoryCache cache
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
		/// Determines the number of tokens that exist in the database.
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
				.Query<TToken>()
				.LongCountAsync(cancellationToken);
		}

		/// <summary>
		/// Determines the number of tokens that match the specified query.
		/// </summary>
		/// <typeparam name="TResult">The result type.</typeparam>
		/// <param name="query">The query to execute.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the number of tokens that match the specified query.
		/// </returns>
		public virtual async ValueTask<long> CountAsync<TResult>(Func<IQueryable<TToken>, IQueryable<TResult>> query, CancellationToken cancellationToken)
		{
			if (query == null)
			{
				throw new ArgumentNullException(nameof(query));
			}

			var session = await this.Context.GetSessionAsync(cancellationToken);

			return await query
				.Invoke(session.Query<TToken>())
				.LongCountAsync(cancellationToken);
		}

		/// <summary>
		/// Creates a new token.
		/// </summary>
		/// <param name="token">The token to create.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual async ValueTask CreateAsync(TToken token, CancellationToken cancellationToken)
		{
			if (token == null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			var session = await this.Context.GetSessionAsync(cancellationToken);

			await session.SaveAsync(token, cancellationToken);
			await session.FlushAsync(cancellationToken);
		}

		/// <summary>
		/// Removes a token.
		/// </summary>
		/// <param name="token">The token to delete.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual async ValueTask DeleteAsync(TToken token, CancellationToken cancellationToken)
		{
			if (token == null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			var session = await this.Context.GetSessionAsync(cancellationToken);

			try
			{
				await session.DeleteAsync(token, cancellationToken);
				await session.FlushAsync(cancellationToken);
			}

			catch (StaleObjectStateException exception)
			{
				var message = new StringBuilder()
					.AppendLine("The token was concurrently updated and cannot be persisted in its current state.")
					.Append("Reload the token from the database and retry the operation.")
					.ToString();

				throw new OpenIddictExceptions.ConcurrencyException(message
					, exception
				);
			}
		}

		/// <summary>
		/// Retrieves the tokens matching the specified parameters.
		/// </summary>
		/// <param name="subject">The subject associated with the token.</param>
		/// <param name="client">The client associated with the token.</param>
		/// <param name="status">The token status.</param>
		/// <param name="type">The token type.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>The tokens corresponding to the criteria.</returns>
		public virtual async IAsyncEnumerable<TToken> FindAsync(string? subject
			, string? client
			, string? status
			, string? type
			, [EnumeratorCancellation] CancellationToken cancellationToken
		)
		{
			using var session = await this.Context.GetSessionAsync(cancellationToken);
			var query = session.Query<TToken>();

			query = query
				.Fetch(token => token.Application)
				.Fetch(token => token.Authorization);

			if (!string.IsNullOrEmpty(subject))
			{
				query = query.Where(token => token.Subject == subject);
			}

			if (!string.IsNullOrEmpty(client))
			{
				var key = this.ConvertIdentifierFromString(client);
				query = query.Where(token => token.Application != null && token.Application.Id!.Equals(key));
			}

			if (!string.IsNullOrEmpty(status))
			{
				query = query.Where(token => token.Status == status);
			}

			if (!string.IsNullOrEmpty(type))
			{
				query = query.Where(token => token.Type == type);
			}

			await foreach (var token in query.AsAsyncEnumerable(cancellationToken))
			{
				yield return token;
			}
		}

		/// <summary>
		/// Retrieves the list of tokens corresponding to the specified application identifier.
		/// </summary>
		/// <param name="identifier">The application identifier associated with the tokens.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>The tokens corresponding to the specified application.</returns>
		public virtual IAsyncEnumerable<TToken> FindByApplicationIdAsync(string identifier, CancellationToken cancellationToken)
		{
			if (string.IsNullOrEmpty(identifier))
			{
				throw new ArgumentException("The identifier cannot be null or empty.", nameof(identifier));
			}

			return ExecuteAsync(cancellationToken);

			async IAsyncEnumerable<TToken> ExecuteAsync([EnumeratorCancellation] CancellationToken ct)
			{
				var session = await this.Context.GetSessionAsync(ct);
				var key = this.ConvertIdentifierFromString(identifier);

				var tokens = session.Query<TToken>()
					.Fetch(token => token.Application)
					.Fetch(token => token.Authorization)
					.Where(token => token.Application != null && token.Application.Id.Equals(key))
					.AsAsyncEnumerable(ct);

				await foreach (var token in tokens)
				{
					yield return token;
				}
			}
		}

		/// <summary>
		/// Retrieves the list of tokens corresponding to the specified authorization identifier.
		/// </summary>
		/// <param name="identifier">The authorization identifier associated with the tokens.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>The tokens corresponding to the specified authorization.</returns>
		public virtual IAsyncEnumerable<TToken> FindByAuthorizationIdAsync(string identifier, CancellationToken cancellationToken)
		{
			if (string.IsNullOrEmpty(identifier))
			{
				throw new ArgumentException("The identifier cannot be null or empty.", nameof(identifier));
			}

			return ExecuteAsync(cancellationToken);

			async IAsyncEnumerable<TToken> ExecuteAsync([EnumeratorCancellation] CancellationToken ct)
			{
				var session = await this.Context.GetSessionAsync(ct);
				var key = this.ConvertIdentifierFromString(identifier);

				var openIddictTokens = session.Query<TToken>()
					.Fetch(token => token.Application)
					.Fetch(token => token.Authorization)
					.Where(token => token.Authorization != null && token.Authorization.Id.Equals(key))
					.AsAsyncEnumerable(ct);

				await foreach (var token in openIddictTokens)
				{
					yield return token;
				}
			}
		}

		/// <summary>
		/// Retrieves a token using its unique identifier.
		/// </summary>
		/// <param name="identifier">The unique identifier associated with the token.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the token corresponding to the unique identifier.
		/// </returns>
		public virtual async ValueTask<TToken?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
		{
			if (string.IsNullOrEmpty(identifier))
			{
				throw new ArgumentException("The identifier cannot be null or empty.", nameof(identifier));
			}

			var session = await this.Context.GetSessionAsync(cancellationToken);

			return await session
				.GetAsync<TToken>(this.ConvertIdentifierFromString(identifier), cancellationToken);
		}

		/// <summary>
		/// Retrieves the list of tokens corresponding to the specified reference identifier.
		/// Note: the reference identifier may be hashed or encrypted for security reasons.
		/// </summary>
		/// <param name="identifier">The reference identifier associated with the tokens.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the tokens corresponding to the specified reference identifier.
		/// </returns>
		public virtual async ValueTask<TToken?> FindByReferenceIdAsync(string identifier, CancellationToken cancellationToken)
		{
			if (string.IsNullOrEmpty(identifier))
			{
				throw new ArgumentException("The identifier cannot be null or empty.", nameof(identifier));
			}

			var session = await this.Context.GetSessionAsync(cancellationToken);

			return await session
				.Query<TToken>()
				.Fetch(token => token.Application)
				.Fetch(token => token.Authorization)
				.Where(token => token.ReferenceId == identifier)
				.FirstOrDefaultAsync(cancellationToken);
		}

		/// <summary>
		/// Retrieves the list of tokens corresponding to the specified subject.
		/// </summary>
		/// <param name="subject">The subject associated with the tokens.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>The tokens corresponding to the specified subject.</returns>
		public virtual IAsyncEnumerable<TToken> FindBySubjectAsync(string subject, CancellationToken cancellationToken)
		{
			if (string.IsNullOrEmpty(subject))
			{
				throw new ArgumentException("The subject cannot be null or empty.", nameof(subject));
			}

			return ExecuteAsync(cancellationToken);

			async IAsyncEnumerable<TToken> ExecuteAsync([EnumeratorCancellation] CancellationToken ct)
			{
				var session = await this.Context.GetSessionAsync(ct);

				var tokens = session.Query<TToken>()
					.Fetch(token => token.Application)
					.Fetch(token => token.Authorization)
					.Where(token => token.Subject == subject).AsAsyncEnumerable(ct);

				await foreach (var token in tokens)
				{
					yield return token;
				}
			}
		}

		/// <summary>
		/// Retrieves the optional application identifier associated with a token.
		/// </summary>
		/// <param name="token">The token.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the application identifier associated with the token.
		/// </returns>
		public virtual ValueTask<string?> GetApplicationIdAsync(TToken token, CancellationToken cancellationToken)
		{
			if (token == null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			if (token.Application == null)
			{
				return new ValueTask<string?>(result: null);
			}

			return new ValueTask<string?>(this.ConvertIdentifierToString(token.Application.Id));
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
		public virtual async ValueTask<TResult?> GetAsync<TState, TResult>(Func<IQueryable<TToken>, TState, IQueryable<TResult>> query
			, TState state
			, CancellationToken cancellationToken
		)
		{
			if (query == null)
			{
				throw new ArgumentNullException(nameof(query));
			}

			var session = await this.Context.GetSessionAsync(cancellationToken);

			var tokens = session
				.Query<TToken>()
				.Fetch(token => token.Application)
				.Fetch(token => token.Authorization);

			return await query
				.Invoke(tokens, state)
				.FirstOrDefaultAsync(cancellationToken);
		}

		/// <summary>
		/// Retrieves the optional authorization identifier associated with a token.
		/// </summary>
		/// <param name="token">The token.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the authorization identifier associated with the token.
		/// </returns>
		public virtual ValueTask<string?> GetAuthorizationIdAsync(TToken token, CancellationToken cancellationToken)
		{
			if (token == null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			if (token.Authorization == null)
			{
				return new ValueTask<string?>(result: null);
			}

			return new ValueTask<string?>(this.ConvertIdentifierToString(token.Authorization.Id));
		}

		/// <summary>
		/// Retrieves the creation date associated with a token.
		/// </summary>
		/// <param name="token">The token.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the creation date associated with the specified token.
		/// </returns>
		public virtual ValueTask<DateTimeOffset?> GetCreationDateAsync(TToken token, CancellationToken cancellationToken)
		{
			if (token == null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			return new ValueTask<DateTimeOffset?>(token.CreationDate);
		}

		/// <summary>
		/// Retrieves the expiration date associated with a token.
		/// </summary>
		/// <param name="token">The token.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the expiration date associated with the specified token.
		/// </returns>
		public virtual ValueTask<DateTimeOffset?> GetExpirationDateAsync(TToken token, CancellationToken cancellationToken)
		{
			if (token == null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			return new ValueTask<DateTimeOffset?>(token.ExpirationDate);
		}

		/// <summary>
		/// Retrieves the unique identifier associated with a token.
		/// </summary>
		/// <param name="token">The token.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the unique identifier associated with the token.
		/// </returns>
		public virtual ValueTask<string?> GetIdAsync(TToken token, CancellationToken cancellationToken)
		{
			if (token == null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			return new ValueTask<string?>(this.ConvertIdentifierToString(token.Id));
		}

		/// <summary>
		/// Retrieves the payload associated with a token.
		/// </summary>
		/// <param name="token">The token.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the payload associated with the specified token.
		/// </returns>
		public virtual ValueTask<string?> GetPayloadAsync(TToken token, CancellationToken cancellationToken)
		{
			if (token == null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			return new ValueTask<string?>(token.Payload);
		}

		/// <summary>
		/// Retrieves the additional properties associated with a token.
		/// </summary>
		/// <param name="token">The token.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns all the additional properties associated with the token.
		/// </returns>
		public virtual ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(TToken token, CancellationToken cancellationToken)
		{
			if (token is null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			if (string.IsNullOrEmpty(token.Properties))
			{
				return new ValueTask<ImmutableDictionary<string, JsonElement>>(ImmutableDictionary.Create<string, JsonElement>());
			}

			// Note: parsing the stringified properties is an expensive operation.
			// To mitigate that, the resulting object is stored in the memory cache.
			var key = string.Concat("0783DFAC-EBC8-43D7-8D2E-FDFEB52A13AB", "\x1e", token.Properties);
			var properties = this.Cache.GetOrCreate(key, entry =>
			{
				entry
					.SetPriority(CacheItemPriority.High)
					.SetSlidingExpiration(TimeSpan.FromMinutes(1));

				using var document = JsonDocument.Parse(token.Properties);
				var builder = ImmutableDictionary.CreateBuilder<string, JsonElement>();

				foreach (var property in document.RootElement.EnumerateObject())
				{
					builder[property.Name] = property.Value.Clone();
				}

				return builder.ToImmutable();
			})!;

			return new ValueTask<ImmutableDictionary<string, JsonElement>>(properties);
		}

		public ValueTask<DateTimeOffset?> GetRedemptionDateAsync(TToken token, CancellationToken cancellationToken)
		{
			if (token is null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			if (token.RedemptionDate is null)
			{
				return new ValueTask<DateTimeOffset?>(result: null);
			}

			return new ValueTask<DateTimeOffset?>(DateTime.SpecifyKind(token.RedemptionDate.Value, DateTimeKind.Utc));
		}

		/// <summary>
		/// Retrieves the reference identifier associated with a token.
		/// Note: depending on the manager used to create the token,
		/// the reference identifier may be hashed for security reasons.
		/// </summary>
		/// <param name="token">The token.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the reference identifier associated with the specified token.
		/// </returns>
		public virtual ValueTask<string?> GetReferenceIdAsync(TToken token, CancellationToken cancellationToken)
		{
			if (token == null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			return new ValueTask<string?>(token.ReferenceId);
		}

		/// <summary>
		/// Retrieves the status associated with a token.
		/// </summary>
		/// <param name="token">The token.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the status associated with the specified token.
		/// </returns>
		public virtual ValueTask<string?> GetStatusAsync(TToken token, CancellationToken cancellationToken)
		{
			if (token == null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			return new ValueTask<string?>(token.Status);
		}

		/// <summary>
		/// Retrieves the subject associated with a token.
		/// </summary>
		/// <param name="token">The token.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the subject associated with the specified token.
		/// </returns>
		public virtual ValueTask<string?> GetSubjectAsync(TToken token, CancellationToken cancellationToken)
		{
			if (token == null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			return new ValueTask<string?>(token.Subject);
		}

		/// <summary>
		/// Retrieves the token type associated with a token.
		/// </summary>
		/// <param name="token">The token.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the token type associated with the specified token.
		/// </returns>
		public virtual ValueTask<string?> GetTypeAsync(TToken token, CancellationToken cancellationToken)
		{
			if (token == null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			return new ValueTask<string?>(token.Type);
		}

		/// <summary>
		/// Instantiates a new token.
		/// </summary>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>
		/// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
		/// whose result returns the instantiated token, that can be persisted in the database.
		/// </returns>
		public virtual ValueTask<TToken> InstantiateAsync(CancellationToken cancellationToken)
		{
			try
			{
				return new ValueTask<TToken>(Activator.CreateInstance<TToken>());
			}

			catch (MemberAccessException exception)
			{
				var message = new StringBuilder()
					.AppendLine("An error occurred while trying to create a new token instance.")
					.Append("Make sure that the token entity is not abstract and has a public parameterless constructor ")
					.Append("or create a custom token store that overrides 'InstantiateAsync()' to use a custom factory.")
					.ToString();

				return new ValueTask<TToken>(Task.FromException<TToken>(
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
		public virtual async IAsyncEnumerable<TToken> ListAsync(int? count
			, int? offset
			, [EnumeratorCancellation] CancellationToken cancellationToken
		)
		{
			var session = await this.Context.GetSessionAsync(cancellationToken);

			var query = session
				.Query<TToken>()
				.Fetch(token => token.Application)
				.Fetch(token => token.Authorization)
				.OrderBy(token => token.Id)
				.AsQueryable();

			if (offset.HasValue)
			{
				query = query.Skip(offset.Value);
			}

			if (count.HasValue)
			{
				query = query.Take(count.Value);
			}

			await foreach (var token in query.AsAsyncEnumerable(cancellationToken))
			{
				yield return token;
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
		public virtual IAsyncEnumerable<TResult> ListAsync<TState, TResult>(Func<IQueryable<TToken>, TState, IQueryable<TResult>> query
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

				var tokens = session
					.Query<TToken>()
					.Fetch(token => token.Application)
					.Fetch(token => token.Authorization);

				var elements = query
					.Invoke(tokens, state)
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
					.Query<TToken>()
					.Where(token => token.CreationDate < date)
					.Where(token => (token.Status != OpenIddictConstants.Statuses.Inactive && token.Status != OpenIddictConstants.Statuses.Valid)
						|| (token.Authorization != null && token.Authorization.Status != OpenIddictConstants.Statuses.Valid)
						|| token.ExpirationDate < DateTime.UtcNow
					)
					.OrderBy(token => token.Id)
					.DeleteAsync(cancellationToken);

				await session.FlushAsync(cancellationToken);
				await transaction.CommitAsync(cancellationToken);

				return deletedEntries;
			}
			catch (StaleObjectStateException exception)
			{
				throw new OpenIddictExceptions.ConcurrencyException("An error occurred while pruning tokens."
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

			var query = session.Query<TToken>();

			query = query
				.Fetch(authorization => authorization.Application)
				.Fetch(authorization => authorization.Authorization);

			if (!string.IsNullOrEmpty(subject))
			{
				query = query.Where(authorization => authorization.Subject == subject);
			}

			if (!string.IsNullOrEmpty(client))
			{
				var key = this.ConvertIdentifierFromString(client);

				query = query.Where(authorization => authorization.Application != null && authorization.Application!.Id!.Equals(key));
			}

			if (!string.IsNullOrEmpty(status))
			{
				query = query.Where(authorization => authorization.Status == status);
			}

			if (!string.IsNullOrEmpty(type))
			{
				query = query.Where(authorization => authorization.Type == type);
			}

			List<Exception>? exceptions = null;

			var result = 0L;

			foreach (var token in await query.ToListAsync(cancellationToken))
			{
				token.Status = OpenIddictConstants.Statuses.Revoked;

				try
				{
					await session.UpdateAsync(token, cancellationToken);
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

		public async ValueTask<long> RevokeByApplicationIdAsync(string identifier, CancellationToken cancellationToken = new CancellationToken())
		{
			ArgumentException.ThrowIfNullOrEmpty(identifier);

			var key = this.ConvertIdentifierFromString(identifier);

			List<Exception>? exceptions = null;

			var result = 0L;

			var session = await this.Context.GetSessionAsync(cancellationToken);

			var tokens = await session
				.Query<TToken>()
				.Fetch(authorization => authorization.Application)
				.Fetch(authorization => authorization.Authorization)
				.Where(authorization => authorization.Application!.Id!.Equals(key))
				.ToListAsync(cancellationToken);

			foreach (var token in tokens)
			{
				token.Status = OpenIddictConstants.Statuses.Revoked;

				try
				{
					await session.UpdateAsync(token, cancellationToken);
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

		public async ValueTask<long> RevokeByAuthorizationIdAsync(string identifier, CancellationToken cancellationToken)
		{
			ArgumentException.ThrowIfNullOrEmpty(identifier);

			var key = this.ConvertIdentifierFromString(identifier);

			List<Exception>? exceptions = null;

			var result = 0L;

			var session = await this.Context.GetSessionAsync(cancellationToken);

			var tokens = await session
				.Query<TToken>()
				.Fetch(authorization => authorization.Application)
				.Fetch(authorization => authorization.Authorization)
				.Where(authorization => authorization.Authorization!.Id!.Equals(key))
				.ToListAsync(cancellationToken);

			foreach (var token in tokens)
			{
				token.Status = OpenIddictConstants.Statuses.Revoked;

				try
				{
					await session.UpdateAsync(token, cancellationToken);
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

		public async ValueTask<long> RevokeBySubjectAsync(string subject, CancellationToken cancellationToken = new CancellationToken())
		{
			ArgumentException.ThrowIfNullOrEmpty(subject);

			List<Exception>? exceptions = null;

			var result = 0L;

			var session = await this.Context.GetSessionAsync(cancellationToken);

			var tokens = await session
				.Query<TToken>()
				.Fetch(authorization => authorization.Application)
				.Fetch(authorization => authorization.Authorization)
				.Where(authorization => authorization.Subject == subject)
				.ToListAsync(cancellationToken);

			foreach (var token in tokens)
			{
				token.Status = OpenIddictConstants.Statuses.Revoked;

				try
				{
					await session.UpdateAsync(token, cancellationToken);
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
		/// Sets the application identifier associated with a token.
		/// </summary>
		/// <param name="token">The token.</param>
		/// <param name="identifier">The unique identifier associated with the client application.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual async ValueTask SetApplicationIdAsync(TToken token, string? identifier, CancellationToken cancellationToken)
		{
			if (token == null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			var session = await this.Context.GetSessionAsync(cancellationToken);

			if (!string.IsNullOrEmpty(identifier))
			{
				token.Application = await session.LoadAsync<TApplication>(this.ConvertIdentifierFromString(identifier), cancellationToken);
			}
			else
			{
				token.Application = null;
			}
		}

		/// <summary>
		/// Sets the authorization identifier associated with a token.
		/// </summary>
		/// <param name="token">The token.</param>
		/// <param name="identifier">The unique identifier associated with the authorization.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual async ValueTask SetAuthorizationIdAsync(TToken token, string? identifier, CancellationToken cancellationToken)
		{
			if (token == null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			var session = await this.Context.GetSessionAsync(cancellationToken);

			if (!string.IsNullOrEmpty(identifier))
			{
				token.Authorization = await session.LoadAsync<TAuthorization>(this.ConvertIdentifierFromString(identifier), cancellationToken);
			}

			else
			{
				token.Authorization = null;
			}
		}

		/// <summary>
		/// Sets the creation date associated with a token.
		/// </summary>
		/// <param name="token">The token.</param>
		/// <param name="date">The creation date.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetCreationDateAsync(TToken token, DateTimeOffset? date, CancellationToken cancellationToken)
		{
			if (token is null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			token.CreationDate = date?.UtcDateTime;

			return default;
		}

		/// <summary>
		/// Sets the expiration date associated with a token.
		/// </summary>
		/// <param name="token">The token.</param>
		/// <param name="date">The expiration date.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetExpirationDateAsync(TToken token, DateTimeOffset? date, CancellationToken cancellationToken)
		{
			if (token == null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			token.ExpirationDate = date?.UtcDateTime;

			return default;
		}

		/// <summary>
		/// Sets the payload associated with a token.
		/// </summary>
		/// <param name="token">The token.</param>
		/// <param name="payload">The payload associated with the token.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetPayloadAsync(TToken token, string? payload, CancellationToken cancellationToken)
		{
			if (token == null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			token.Payload = payload;

			return default;
		}

		/// <summary>
		/// Sets the additional properties associated with a token.
		/// </summary>
		/// <param name="token">The token.</param>
		/// <param name="properties">The additional properties associated with the token.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetPropertiesAsync(TToken token, ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
		{
			if (token is null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			if (properties is not { Count: > 0 })
			{
				token.Properties = null;

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

			token.Properties = Encoding.UTF8.GetString(stream.ToArray());

			return default;
		}

		public ValueTask SetRedemptionDateAsync(TToken token, DateTimeOffset? date, CancellationToken cancellationToken)
		{
			if (token is null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			token.RedemptionDate = date?.UtcDateTime;

			return default;
		}

		/// <summary>
		/// Sets the reference identifier associated with a token.
		/// Note: depending on the manager used to create the token,
		/// the reference identifier may be hashed for security reasons.
		/// </summary>
		/// <param name="token">The token.</param>
		/// <param name="identifier">The reference identifier associated with the token.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetReferenceIdAsync(TToken token, string? identifier, CancellationToken cancellationToken)
		{
			if (token == null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			token.ReferenceId = identifier;

			return default;
		}

		/// <summary>
		/// Sets the status associated with a token.
		/// </summary>
		/// <param name="token">The token.</param>
		/// <param name="status">The status associated with the authorization.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetStatusAsync(TToken token, string? status, CancellationToken cancellationToken)
		{
			if (token == null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			token.Status = status;

			return default;
		}

		/// <summary>
		/// Sets the subject associated with a token.
		/// </summary>
		/// <param name="token">The token.</param>
		/// <param name="subject">The subject associated with the token.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetSubjectAsync(TToken token, string? subject, CancellationToken cancellationToken)
		{
			if (token == null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			token.Subject = subject;

			return default;
		}

		/// <summary>
		/// Sets the token type associated with a token.
		/// </summary>
		/// <param name="token">The token.</param>
		/// <param name="type">The token type associated with the token.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual ValueTask SetTypeAsync(TToken token, string? type, CancellationToken cancellationToken)
		{
			if (token == null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			token.Type = type;

			return default;
		}

		/// <summary>
		/// Updates an existing token.
		/// </summary>
		/// <param name="token">The token to update.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
		/// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
		public virtual async ValueTask UpdateAsync(TToken token, CancellationToken cancellationToken)
		{
			if (token == null)
			{
				throw new ArgumentNullException(nameof(token));
			}

			var session = await this.Context.GetSessionAsync(cancellationToken);

			try
			{
				await session.UpdateAsync(token, cancellationToken);
				await session.FlushAsync(cancellationToken);
			}

			catch (StaleObjectStateException exception)
			{
				var message = new StringBuilder()
					.AppendLine("The token was concurrently updated and cannot be persisted in its current state.")
					.Append("Reload the token from the database and retry the operation.")
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
