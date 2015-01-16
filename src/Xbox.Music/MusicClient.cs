﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Resources;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PortableRest;

namespace Xbox.Music
{

    /// <summary>
    /// 
    /// </summary>
    public class MusicClient : RestClient
    {

        #region Properties

        /// <summary>
        /// The Client ID assigned to you from your Azure Marketplace account.
        /// </summary>
        public string ClientId { get; private set; }

        /// <summary>
        /// The Client Secret assigned to you from your Azure Marketplace account.
        /// </summary>
        public string ClientSecret { get; private set; }

        /// <summary>
        /// Optional. The two-letter standard code identifying the requested language for the response content. 
        /// If not specified, defaults to the country's primary language.
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Optional. The standard two-letter code that identifies the country/region of the user. 
        /// If not specified, the value defaults to the geolocated country/region of the client's IP address. 
        /// Responses will be filtered to provide only those that match the user's country/region.
        /// </summary>
        public string Country { get; set; }
        
        /// <summary>
        /// Required. A valid developer authentication Access Token obtained from Azure Data Market, 
        /// used to identify the third-party application using the Xbox Music RESTful API.
        /// </summary>
        //private string AccessToken { get; set; }

        /// <summary>
        /// Keeps track of when the token was last issued so the MusicClient can obtain a new one 
        /// before it expires.
        /// </summary>
        //private DateTime TokenLastAcquired { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public TokenResponse TokenResponse { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the MusicClient for a given ClientId and ClientSecret.
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="clientSecret"></param>
        public MusicClient(string clientId, string clientSecret)
        {
            ClientId = clientId;
            ClientSecret = clientSecret;
            BaseUrl = "https://music.xboxlive.com";
        }

        #endregion

        #region Public Methods

        #region Find

        /// <summary>
        /// Allows you to find a <see cref="Artist"/>/<see cref="Album"/>/<see cref="Track"/> by a string query.
        /// </summary>
        /// <param name="query">The term to search for.</param>
        /// <param name="maxItems">The maximum number of results per page to return.</param>
        /// <param name="getArtists">Specifies whether or not to include <see cref="Artist">Artists</see> in the results. Defaults to true.</param>
        /// <param name="getAlbums">Specifies whether or not to include <see cref="Album">Albums</see> in the results. Defaults to true.</param>
        /// <param name="getTracks">Specifies whether or not to include <see cref="Track">Tracks</see> in the results. Defaults to true.</param>
        /// <returns>A <see cref="ContentResponse"/> object populated with results from the Xbox Music service.</returns>
        /// <exception cref="ArgumentOutOfRangeException">This exception is thrown if you try to ask for more than 25 items.</exception>
        public async Task<ContentResponse> Find(string query, int maxItems = 25, bool getArtists = true, bool getAlbums = true, bool getTracks = true)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException("query", "You must specify something to search for.");

            if (maxItems > 25)
                throw new ArgumentOutOfRangeException("maxItems", "Value cannot be greater than 25.");

            await CheckToken();

            var request = GetPopulatedRequest("1/content/{namespace}/search");

            request.AddQueryString("q", query);

            if (maxItems != 25)
            {
                request.AddQueryString("maxItems", maxItems);
            }

            if (!(getArtists && getAlbums && getTracks))
            {
                var filter = new List<string>();

                if (getArtists) filter.Add("artists");
                if (getAlbums) filter.Add("albums");
                if (getTracks) filter.Add("tracks");

                request.AddQueryString("filters", filter.Aggregate("", (c, n) => c.Length == 0 ? c += n : c += "+" + n));
            }

            request.AddQueryString("accessToken", "Bearer " + TokenResponse.AccessToken);

            return await ExecuteAsync<ContentResponse>(request);
        }

        /// <summary>
        /// Allows you to continue a previous search request.
        /// </summary>
        /// <param name="query">The term to search for.</param>
        /// <param name="continuationToken"></param>
        /// <returns>A <see cref="ContentResponse"/> object populated with results from the Xbox Music service.</returns>
        /// <exception cref="ArgumentOutOfRangeException">This exception is thrown if you try to ask for more than 25 items.</exception>
        public async Task<ContentResponse> Find(string query, string continuationToken)
        {
            if (string.IsNullOrWhiteSpace(continuationToken))
                throw new ArgumentNullException("continuationToken", "You must specify the continuationToken from a previous query.");

            //When there's a continuationToken there must not be Language nor Country in the request
            Language = null;
            Country = null;

            await CheckToken();
            var request = GetPopulatedRequest("1/content/{namespace}/search");

            request.AddQueryString("continuationToken", continuationToken);
            request.AddQueryString("accessToken", "Bearer " + TokenResponse.AccessToken);
            
            return await ExecuteAsync<ContentResponse>(request);
        }

        #endregion

        #region Get

        /// <summary>
        /// Allows you to get an <see cref="Artist"/>/<see cref="Album"/>/<see cref="Track"/> by a known identifier. 
        /// </summary>
        /// <param name="id">The ID to search for. Must start with "music."</param>
        /// <param name="options"></param>
        /// <returns>A <see cref="ContentResponse"/> object populated with results from the Xbox Music service.</returns>
        public async Task<ContentResponse> Get(string id, LookupOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException("id", "You must specify an ID");

            return await Get(new List<string> {id}, options);
        }

        /// <summary>
        /// Allows you to get multiple <see cref="Artist">Artists</see>/<see cref="Album">Albums</see>/<see cref="Track">Tracks</see> by known identifiers. 
        /// </summary>
        /// <param name="ids">A List of IDs to search for. Must start with "music."</param>
        /// <param name="options">Optional. A <see cref="LookupOptions"/> instance with details on what additional information should be returned.</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <returns>A <see cref="ContentResponse"/> object populated with results from the Xbox Music service.</returns>
        public async Task<ContentResponse> Get(List<string> ids, LookupOptions options = null)
        {
            if (ids == null)
                throw new ArgumentNullException("ids", "You must pass in a list of IDs to lookup.");

            if (ids.Count == 0)
                throw new ArgumentOutOfRangeException("ids", "The list of IDs to lookup cannot be empty.");

            await CheckToken();

            var request = GetPopulatedRequest("1/content/{ids}/lookup");
            request.AddUrlSegment("ids", ids.Aggregate("", (c, n) => c.Length == 0 ? c += n : c += "+" + n));

            if (options != null)
            {
                var extras = new List<string>();
                if (options.GetArtistAlbums)
                {
                    extras.Add("albums");
                }
                if (options.GetArtistTopTracks)
                {
                    extras.Add("topTracks");
                }
                if (options.GetAlbumTracks)
                {
                    extras.Add("tracks");
                }
                if (options.GetAlbumArtistDetails || options.GetTrackArtistDetails)
                {
                    extras.Add("artistDetails");
                }
                if (options.GetTrackAlbumDetails)
                {
                    extras.Add("albumDetails");
                }
                request.AddQueryString("extras", extras.Aggregate("", (c, n) => c.Length == 0 ? c += n : c += "+" + n));
            }

            request.AddQueryString("accessToken", "Bearer " + TokenResponse.AccessToken);
            
            return await ExecuteAsync<ContentResponse>(request);
        }

        /// <summary>
        /// Allows you to get an <see cref="Artist"/>/<see cref="Album"/>/<see cref="Track"/> by a known identifier, 
        /// and the ContinuationToken from a previous request.
        /// </summary>
        /// <param name="id">The ID to search for. Must start with "music."</param>
        /// <param name="continuationToken"></param>
        /// <returns></returns>
        public async Task<ContentResponse> Get(string id, string continuationToken)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException("id", "You must specify an ID");

            if (string.IsNullOrWhiteSpace(continuationToken))
                throw new ArgumentNullException("continuationToken", "You must specify the continuationToken from a previous query.");

            return await Get(new List<string> { id }, continuationToken);
        }

        /// <summary>
        /// Allows you to get multiple <see cref="Artist">Artists</see>/<see cref="Album">Albums</see>/<see cref="Track">Tracks</see> by known identifiers,
        /// and the ContinuationToken from a previous request.
        /// </summary>
        /// <param name="ids">A List of IDs to search for. Must start with "music."</param>
        /// <param name="continuationToken">The PaginatedList.ContinuationToken from a previous request.</param>
        /// <returns></returns>
        public async Task<ContentResponse> Get(List<string> ids, string continuationToken)
        {
            if (ids == null)
                throw new ArgumentNullException("ids", "You must pass in a list of IDs to lookup.");

            if (ids.Count == 0)
                throw new ArgumentOutOfRangeException("ids", "The list of IDs to lookup cannot be empty.");

            if (string.IsNullOrWhiteSpace(continuationToken))
                throw new ArgumentNullException("continuationToken", "You must specify the continuationToken from a previous query.");

            await CheckToken();

            var request = GetPopulatedRequest("1/content/{ids}/lookup");
            request.AddUrlSegment("ids", ids.Aggregate("", (c, n) => c.Length == 0 ? c += n : c += "+" + n));
            request.AddQueryString("continuationToken", continuationToken);
            request.AddQueryString("accessToken", "Bearer " + TokenResponse.AccessToken);

            return await ExecuteAsync<ContentResponse>(request);
        }

        #endregion

        #endregion

        #region Private Methods

        /// <summary>
        /// Gets a new <see cref="RestRequest"/> populated with the common values for every request.
        /// </summary>
        /// <param name="resourceUrl"></param>
        /// <returns>A new <see cref="RestRequest"/> populated with the common values for every request</returns>
        protected virtual RestRequest GetPopulatedRequest(string resourceUrl)
        {
            if (string.IsNullOrWhiteSpace(TokenResponse.AccessToken))
            {
                throw new Exception("The Xbox Music Client was unable to obtain an AccessToken from the authentication service.");
            }

            var request = new RestRequest(resourceUrl) { ContentType = ContentTypes.Json };

            request.AddUrlSegment("namespace", "music");

            if (!string.IsNullOrWhiteSpace(Language))
            {
                request.AddQueryString("language", Language);
            }
            if (!string.IsNullOrWhiteSpace(Country))
            {
                request.AddQueryString("country", Country);
            }
           
            return request;
        }

        /// <summary>
        /// Checks to see if the token needs to be acquired or refreshed. If the Token is null or invalid, 
        /// it will block the calling method until the Token request completes. Otherwise it will be considered
        /// a proactive refresh and get an updated token in the background.
        /// </summary>
        /// <returns></returns>
        protected async Task CheckToken()
        {
            if (TokenResponse != null && TokenResponse.NeedsRefresh)
            {
                // RWM: The token is still valid but within the 30 refresh window. 
                // Get a new token, but to not block the existing request.
                Debug.WriteLine("Proactively refreshing the AccessToken...");
#pragma warning disable 4014
                Authenticate();
#pragma warning restore 4014
            }

            if (TokenResponse == null || !TokenResponse.IsValid)
            {
                // RWM: The token is invalid or outside the refresh window. 
                // Get a new token, blocking the waiting request until the token has been acquired.
                Debug.WriteLine("Obtaining an AccessToken...");
                await Authenticate();
            }
        }

        /// <summary>
        /// Acquires a new AuthToken from Azure Access Control.
        /// </summary>
        /// <returns></returns>
        protected async Task Authenticate()
        {
            var client = new RestClient
            {
                BaseUrl = "https://datamarket.accesscontrol.windows.net",
                UserAgent = UserAgent,
            };
            var request = new RestRequest("v2/OAuth2-13", HttpMethod.Post)
            {
                ContentType = ContentTypes.FormUrlEncoded,
                ReturnRawString = true,
            };
            request.AddParameter("client_id", ClientId);
            request.AddParameter("client_secret", ClientSecret);
            request.AddParameter("scope", "http://music.xboxlive.com");
            request.AddParameter("grant_type", "client_credentials");

            var result = await client.ExecuteAsync<string>(request);

            TokenResponse = JsonConvert.DeserializeObject<TokenResponse>(result);
            if (TokenResponse != null)
            {
                TokenResponse.TimeStamp = DateTime.Now;
            }

            //var token = Regex.Match(result, ".*\"access_token\":\"(.*?)\".*", RegexOptions.IgnoreCase).Groups[1].Value;
            //AccessToken = token;
            //TokenLastAcquired = DateTime.Now;
        }

        #endregion

    }
}
