using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace OpenIddict.NHibernate.Models
{
    /// <summary>
    /// Represents an OpenIddict application.
    /// </summary>
    public class OpenIddictApplication : OpenIddictApplication<string, OpenIddictAuthorization, OpenIddictToken>
    {
        public OpenIddictApplication()
        {
            // Generate a new string identifier.
            Id = Guid.NewGuid().ToString();
        }
    }

    /// <summary>
    /// Represents an OpenIddict application.
    /// </summary>
    public class OpenIddictApplication<TKey> : OpenIddictApplication<TKey, OpenIddictAuthorization<TKey>, OpenIddictToken<TKey>>
        where TKey : IEquatable<TKey>
    { }

    /// <summary>
    /// Represents an OpenIddict application.
    /// </summary>
    [DebuggerDisplay("Id = {Id.ToString(),nq} ; ClientId = {ClientId,nq} ; Type = {Type,nq}")]
    public class OpenIddictApplication<TKey, TAuthorization, TToken> where TKey : IEquatable<TKey>
    {
        /// <summary>
        /// Gets or sets the list of the authorizations associated with this application.
        /// </summary>
        public virtual IList<TAuthorization> Authorizations { get; set; } = new List<TAuthorization>();

        /// <summary>
        /// Gets or sets the client identifier
        /// associated with the current application.
        /// </summary>
        public virtual string ClientId { get; set; }

        /// <summary>
        /// Gets or sets the client secret associated with the current application.
        /// Note: depending on the application manager used to create this instance,
        /// this property may be hashed or encrypted for security reasons.
        /// </summary>
        public virtual string ClientSecret { get; set; }

        /// <summary>
        /// Gets or sets the consent type
        /// associated with the current application.
        /// </summary>
        public virtual string ConsentType { get; set; }

        /// <summary>
        /// Gets or sets the display name
        /// associated with the current application.
        /// </summary>
        public virtual string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier
        /// associated with the current application.
        /// </summary>
        public virtual TKey Id { get; set; }

        /// <summary>
        /// Gets or sets the permissions associated with the
        /// current application, serialized as a JSON array.
        /// </summary>
        public virtual string Permissions { get; set; }

        /// <summary>
        /// Gets or sets the logout callback URLs associated with
        /// the current application, serialized as a JSON array.
        /// </summary>
        public virtual string PostLogoutRedirectUris { get; set; }

        /// <summary>
        /// Gets or sets the additional properties serialized as a JSON object,
        /// or <c>null</c> if no bag was associated with the current application.
        /// </summary>
        public virtual string Properties { get; set; }

        /// <summary>
        /// Gets or sets the callback URLs associated with the
        /// current application, serialized as a JSON array.
        /// </summary>
        public virtual string RedirectUris { get; set; }

        /// <summary>
        /// Gets or sets the requirements associated with the
        /// current application, serialized as a JSON array.
        /// </summary>
        public virtual string Requirements { get; set; }

        /// <summary>
        /// Gets or sets the list of the tokens associated with this application.
        /// </summary>
        public virtual IList<TToken> Tokens { get; set; } = new List<TToken>();

        /// <summary>
        /// Gets or sets the application type
        /// associated with the current application.
        /// </summary>
        public virtual string Type { get; set; }

        /// <summary>
        /// Gets or sets the entity version, used as a concurrency token.
        /// </summary>
        public virtual int Version { get; set; }
    }
}
