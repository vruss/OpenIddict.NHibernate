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
    /// Provides methods allowing to manage the tokens stored in a database.
    /// </summary>
    public class OpenIddictTokenStore : OpenIddictTokenStore<OpenIddictToken,
                                                             OpenIddictApplication,
                                                             OpenIddictAuthorization, string>
    {
        public OpenIddictTokenStore(
            IMemoryCache cache,
            IOpenIddictNHibernateContext context,
            IOptionsMonitor<OpenIddictNHibernateOptions> options)
            : base(cache, context, options)
        {
        }
    }

    /// <summary>
    /// Provides methods allowing to manage the tokens stored in a database.
    /// </summary>
    /// <typeparam name="TKey">The type of the entity primary keys.</typeparam>
    public class OpenIddictTokenStore<TKey> : OpenIddictTokenStore<OpenIddictToken<TKey>,
                                                                   OpenIddictApplication<TKey>,
                                                                   OpenIddictAuthorization<TKey>, TKey>
        where TKey : IEquatable<TKey>
    {
        public OpenIddictTokenStore(
            IMemoryCache cache,
            IOpenIddictNHibernateContext context,
            IOptionsMonitor<OpenIddictNHibernateOptions> options)
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
        public OpenIddictTokenStore(
            IMemoryCache cache,
            IOpenIddictNHibernateContext context,
            IOptionsMonitor<OpenIddictNHibernateOptions> options)
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
            return await session.Query<TToken>().LongCountAsync(cancellationToken);
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
            return await query(session.Query<TToken>()).LongCountAsync(cancellationToken);
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
                throw new OpenIddictExceptions.ConcurrencyException(new StringBuilder()
                    .AppendLine("The token was concurrently updated and cannot be persisted in its current state.")
                    .Append("Reload the token from the database and retry the operation.")
                    .ToString(), exception);
            }
        }

        /// <summary>
        /// Retrieves the tokens corresponding to the specified
        /// subject and associated with the application identifier.
        /// </summary>
        /// <param name="subject">The subject associated with the token.</param>
        /// <param name="client">The client associated with the token.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>The tokens corresponding to the subject/client.</returns>
        public virtual IAsyncEnumerable<TToken> FindAsync(string subject,
            string client, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(subject))
            {
                throw new ArgumentException("The subject cannot be null or empty.", nameof(subject));
            }

            if (string.IsNullOrEmpty(client))
            {
                throw new ArgumentException("The client cannot be null or empty.", nameof(client));
            }

            return ExecuteAsync(cancellationToken);

            async IAsyncEnumerable<TToken> ExecuteAsync(CancellationToken cancellationToken)
            {
                var session = await this.Context.GetSessionAsync(cancellationToken);
                var key = this.ConvertIdentifierFromString(client);

                await foreach (var token in (from token in session.Query<TToken>()
                                                                  .Fetch(token => token.Application)
                                                                  .Fetch(token => token.Authorization)
                                             where token.Application != null &&
                                                   token.Application.Id.Equals(key) &&
                                                   token.Subject == subject
                                             select token).AsAsyncEnumerable(cancellationToken))
                {
                    yield return token;
                }
            }
        }

        /// <summary>
        /// Retrieves the tokens matching the specified parameters.
        /// </summary>
        /// <param name="subject">The subject associated with the token.</param>
        /// <param name="client">The client associated with the token.</param>
        /// <param name="status">The token status.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>The tokens corresponding to the criteria.</returns>
        public virtual IAsyncEnumerable<TToken> FindAsync(
            string subject, string client,
            string status, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(subject))
            {
                throw new ArgumentException("The subject cannot be null or empty.", nameof(subject));
            }

            if (string.IsNullOrEmpty(client))
            {
                throw new ArgumentException("The client cannot be null or empty.", nameof(client));
            }

            if (string.IsNullOrEmpty(status))
            {
                throw new ArgumentException("The status cannot be null or empty.", nameof(status));
            }

            return ExecuteAsync(cancellationToken);

            async IAsyncEnumerable<TToken> ExecuteAsync(CancellationToken cancellationToken)
            {
                var session = await this.Context.GetSessionAsync(cancellationToken);
                var key = this.ConvertIdentifierFromString(client);

                await foreach (var token in (from token in session.Query<TToken>()
                                                                  .Fetch(token => token.Application)
                                                                  .Fetch(token => token.Authorization)
                                             where token.Application != null &&
                                                   token.Application.Id.Equals(key) &&
                                                   token.Subject == subject &&
                                                   token.Status == status
                                             select token).AsAsyncEnumerable(cancellationToken))
                {
                    yield return token;
                }
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
        public virtual IAsyncEnumerable<TToken> FindAsync(
            string subject, string client,
            string status, string type, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(subject))
            {
                throw new ArgumentException("The subject cannot be null or empty.", nameof(subject));
            }

            if (string.IsNullOrEmpty(client))
            {
                throw new ArgumentException("The client identifier cannot be null or empty.", nameof(client));
            }

            if (string.IsNullOrEmpty(status))
            {
                throw new ArgumentException("The status cannot be null or empty.", nameof(status));
            }

            if (string.IsNullOrEmpty(type))
            {
                throw new ArgumentException("The type cannot be null or empty.", nameof(type));
            }

            return ExecuteAsync(cancellationToken);

            async IAsyncEnumerable<TToken> ExecuteAsync(CancellationToken cancellationToken)
            {
                var session = await this.Context.GetSessionAsync(cancellationToken);
                var key = this.ConvertIdentifierFromString(client);

                await foreach (var token in (from token in session.Query<TToken>()
                                                                  .Fetch(token => token.Application)
                                                                  .Fetch(token => token.Authorization)
                                             where token.Application != null &&
                                                   token.Application.Id.Equals(key) &&
                                                   token.Subject == subject &&
                                                   token.Status == status &&
                                                   token.Type == type
                                             select token).AsAsyncEnumerable(cancellationToken))
                {
                    yield return token;
                }
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

            async IAsyncEnumerable<TToken> ExecuteAsync(CancellationToken cancellationToken)
            {
                var session = await this.Context.GetSessionAsync(cancellationToken);
                var key = this.ConvertIdentifierFromString(identifier);

                await foreach (var token in (from token in session.Query<TToken>()
                                                                  .Fetch(token => token.Application)
                                                                  .Fetch(token => token.Authorization)
                                             where token.Application != null &&
                                                   token.Application.Id.Equals(key)
                                             select token).AsAsyncEnumerable(cancellationToken))
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

            async IAsyncEnumerable<TToken> ExecuteAsync(CancellationToken cancellationToken)
            {
                var session = await this.Context.GetSessionAsync(cancellationToken);
                var key = this.ConvertIdentifierFromString(identifier);

                await foreach (var token in (from token in session.Query<TToken>()
                                                                  .Fetch(token => token.Application)
                                                                  .Fetch(token => token.Authorization)
                                             where token.Authorization != null &&
                                                   token.Authorization.Id.Equals(key)
                                             select token).AsAsyncEnumerable(cancellationToken))
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
        public virtual async ValueTask<TToken> FindByIdAsync(string identifier, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException("The identifier cannot be null or empty.", nameof(identifier));
            }

            var session = await this.Context.GetSessionAsync(cancellationToken);
            return await session.GetAsync<TToken>(this.ConvertIdentifierFromString(identifier), cancellationToken);
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
        public virtual async ValueTask<TToken> FindByReferenceIdAsync(string identifier, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException("The identifier cannot be null or empty.", nameof(identifier));
            }

            var session = await this.Context.GetSessionAsync(cancellationToken);

            return await (from token in session.Query<TToken>()
                                               .Fetch(token => token.Application)
                                               .Fetch(token => token.Authorization)
                          where token.ReferenceId == identifier
                          select token).FirstOrDefaultAsync(cancellationToken);
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

            async IAsyncEnumerable<TToken> ExecuteAsync(CancellationToken cancellationToken)
            {
                var session = await this.Context.GetSessionAsync(cancellationToken);

                await foreach (var token in (from token in session.Query<TToken>()
                                                                  .Fetch(token => token.Application)
                                                                  .Fetch(token => token.Authorization)
                                             where token.Subject == subject
                                             select token).AsAsyncEnumerable(cancellationToken))
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
        public virtual ValueTask<string> GetApplicationIdAsync(TToken token, CancellationToken cancellationToken)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            if (token.Application == null)
            {
                return new ValueTask<string>(result: null);
            }

            return new ValueTask<string>(this.ConvertIdentifierToString(token.Application.Id));
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
            Func<IQueryable<TToken>, TState, IQueryable<TResult>> query,
            TState state, CancellationToken cancellationToken)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            var session = await this.Context.GetSessionAsync(cancellationToken);

            return await query(
                session.Query<TToken>().Fetch(token => token.Application)
                                       .Fetch(token => token.Authorization), state).FirstOrDefaultAsync(cancellationToken);
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
        public virtual ValueTask<string> GetAuthorizationIdAsync(TToken token, CancellationToken cancellationToken)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            if (token.Authorization == null)
            {
                return new ValueTask<string>(result: null);
            }

            return new ValueTask<string>(this.ConvertIdentifierToString(token.Authorization.Id));
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
        public virtual ValueTask<string> GetIdAsync(TToken token, CancellationToken cancellationToken)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            return new ValueTask<string>(this.ConvertIdentifierToString(token.Id));
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
        public virtual ValueTask<string> GetPayloadAsync(TToken token, CancellationToken cancellationToken)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            return new ValueTask<string>(token.Payload);
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
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            if (string.IsNullOrEmpty(token.Properties))
            {
                return new ValueTask<ImmutableDictionary<string, JsonElement>>(ImmutableDictionary.Create<string, JsonElement>());
            }

            // Note: parsing the stringified properties is an expensive operation.
            // To mitigate that, the resulting object is stored in the memory cache.
            var key = string.Concat("d0509397-1bbf-40e7-97e1-5e6d7bc2536c", "\x1e", token.Properties);
            var properties = this.Cache.GetOrCreate(key, entry =>
            {
                entry.SetPriority(CacheItemPriority.High)
                     .SetSlidingExpiration(TimeSpan.FromMinutes(1));

                return JsonSerializer.Deserialize<ImmutableDictionary<string, JsonElement>>(token.Properties);
            });

            return new ValueTask<ImmutableDictionary<string, JsonElement>>(properties);
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
        public virtual ValueTask<string> GetReferenceIdAsync(TToken token, CancellationToken cancellationToken)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            return new ValueTask<string>(token.ReferenceId);
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
        public virtual ValueTask<string> GetStatusAsync(TToken token, CancellationToken cancellationToken)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            return new ValueTask<string>(token.Status);
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
        public virtual ValueTask<string> GetSubjectAsync(TToken token, CancellationToken cancellationToken)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            return new ValueTask<string>(token.Subject);
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
        public virtual ValueTask<string> GetTypeAsync(TToken token, CancellationToken cancellationToken)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            return new ValueTask<string>(token.Type);
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
                return new ValueTask<TToken>(Task.FromException<TToken>(
                    new InvalidOperationException(new StringBuilder()
                        .AppendLine("An error occurred while trying to create a new token instance.")
                        .Append("Make sure that the token entity is not abstract and has a public parameterless constructor ")
                        .Append("or create a custom token store that overrides 'InstantiateAsync()' to use a custom factory.")
                        .ToString(), exception)));
            }
        }

        /// <summary>
        /// Executes the specified query and returns all the corresponding elements.
        /// </summary>
        /// <param name="count">The number of results to return.</param>
        /// <param name="offset">The number of results to skip.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>All the elements returned when executing the specified query.</returns>
        public virtual async IAsyncEnumerable<TToken> ListAsync(
            int? count, int? offset, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var session = await this.Context.GetSessionAsync(cancellationToken);
            var query = session.Query<TToken>()
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
        public virtual IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
            Func<IQueryable<TToken>, TState, IQueryable<TResult>> query,
            TState state, CancellationToken cancellationToken)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            return ExecuteAsync(cancellationToken);

            async IAsyncEnumerable<TResult> ExecuteAsync(CancellationToken cancellationToken)
            {
                var session = await this.Context.GetSessionAsync(cancellationToken);

                await foreach (var element in query(
                    session.Query<TToken>()
                           .Fetch(token => token.Application)
                           .Fetch(token => token.Authorization), state).AsAsyncEnumerable(cancellationToken))
                {
                    yield return element;
                }
            }
        }

        /// <summary>
        /// Removes the tokens that are marked as expired or invalid.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
        public virtual async ValueTask PruneAsync(CancellationToken cancellationToken)
        {
            var session = await this.Context.GetSessionAsync(cancellationToken);

            await (from token in session.Query<TToken>()
                   where token.Status != OpenIddictConstants.Statuses.Valid ||
                         token.ExpirationDate < DateTimeOffset.UtcNow
                   select token).DeleteAsync(cancellationToken);

            await session.FlushAsync(cancellationToken);
        }

        /// <summary>
        /// Sets the application identifier associated with a token.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="identifier">The unique identifier associated with the client application.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
        public virtual async ValueTask SetApplicationIdAsync(TToken token,
            string identifier, CancellationToken cancellationToken)
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
        public virtual async ValueTask SetAuthorizationIdAsync(TToken token,
            string identifier, CancellationToken cancellationToken)
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
        public virtual ValueTask SetCreationDateAsync(TToken token,
            DateTimeOffset? date, CancellationToken cancellationToken)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            token.CreationDate = date;

            return default;
        }

        /// <summary>
        /// Sets the expiration date associated with a token.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="date">The expiration date.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
        public virtual ValueTask SetExpirationDateAsync(TToken token,
            DateTimeOffset? date, CancellationToken cancellationToken)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            token.ExpirationDate = date;

            return default;
        }

        /// <summary>
        /// Sets the payload associated with a token.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="payload">The payload associated with the token.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
        public virtual ValueTask SetPayloadAsync(TToken token, string payload, CancellationToken cancellationToken)
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
        public virtual ValueTask SetPropertiesAsync(TToken token,
            ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            if (properties == null || properties.IsEmpty)
            {
                token.Properties = null;

                return default;
            }

            token.Properties = JsonSerializer.Serialize(properties, new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = false
            });

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
        public virtual ValueTask SetReferenceIdAsync(TToken token, string identifier, CancellationToken cancellationToken)
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
        public virtual ValueTask SetStatusAsync(TToken token, string status, CancellationToken cancellationToken)
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
        public virtual ValueTask SetSubjectAsync(TToken token, string subject, CancellationToken cancellationToken)
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
        public virtual ValueTask SetTypeAsync(TToken token, string type, CancellationToken cancellationToken)
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
                throw new OpenIddictExceptions.ConcurrencyException(new StringBuilder()
                    .AppendLine("The token was concurrently updated and cannot be persisted in its current state.")
                    .Append("Reload the token from the database and retry the operation.")
                    .ToString(), exception);
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

            return (TKey) TypeDescriptor.GetConverter(typeof(TKey)).ConvertFromInvariantString(identifier);
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

            return TypeDescriptor.GetConverter(typeof(TKey)).ConvertToInvariantString(identifier);
        }
    }
}
