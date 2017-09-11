﻿using Mixer.Base.Model.OAuth;
using Mixer.Base.Services;
using Mixer.Base.Util;
using Mixer.Base.Web;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Mixer.Base
{
    public enum OAuthClientScopeEnum
    {
        achievement__view__self,
        channel__analytics__self,
        channel__costream__self,
        channel__deleteBanner__self,
        channel__details__self,
        channel__follow__self,
        channel__partnership,
        channel__partnership__self,
        channel__streamKey__self,
        channel__update__self,
        chat__bypass_links,
        chat__bypass_slowchat,
        chat__change_ban,
        chat__change_role,
        chat__chat,
        chat__clear_messages,
        chat__connect,
        chat__edit_options,
        chat__giveaway_start,
        chat__poll_start,
        chat__poll_vote,
        chat__purge,
        chat__remove_message,
        chat__timeout,
        chat__view_deleted,
        chat__whisper,
        interactive__manage__self,
        interactive__robot__self,
        invoice__view__self,
        log__view__self,
        notification__update__self,
        notification__view__self,
        recording__manage__self,
        redeemable__create__self,
        redeemable__redeem__self,
        redeemable__view__self,
        resource__find__self,
        subscription__cancel__self,
        subscription__create__self,
        subscription__renew__self,
        subscription__view__self,
        team__administer,
        team__manage__self,
        transaction__cancel__self,
        transaction__view__self,
        type__viewHidden,
        user__analytics__self,
        user__details__self,
        user__getDiscordInvite__self,
        user__log__self,
        user__notification__self,
        user__seen__self,
        user__update__self,
        user__updatePassword__self,
    }

    public class MixerConnection
    {
        public const string OAUTH_LOCALHOST_URL = "http://localhost:8080/";

        private OAuthTokenModel token;

        public ChannelsService Channels { get; private set; }
        public ChatsService Chats { get; private set; }
        public CostreamService Costream { get; private set; }
        public InteractiveService Interactive { get; private set; }
        public OAuthService OAuth { get; private set; }
        public TeamsService Teams { get; private set; }
        public GameTypesService GameTypes { get; private set; }
        public UsersService Users { get; private set; }

        /// <summary>
        /// NOTE: There is a known issue with the Mixer APIs where authenticating with a short code as opposed to the regular OAuth process, where certain
        /// Chat Client commands will not work (EX: Timeout, Clear Messages, Delete Message, etc). The current work around to this is to use the traditional
        /// OAuth authentication methods.
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="scopes"></param>
        /// <param name="codeCallback"></param>
        /// <returns></returns>
        public static async Task<MixerConnection> ConnectViaShortCode(string clientID, IEnumerable<OAuthClientScopeEnum> scopes, Action<OAuthShortCodeModel> codeCallback)
        {
            return await MixerConnection.ConnectViaShortCode(clientID, null, scopes, codeCallback);
        }

        /// <summary>
        /// NOTE: There is a known issue with the Mixer APIs where authenticating with a short code as opposed to the regular OAuth process, where certain
        /// Chat Client commands will not work (EX: Timeout, Clear Messages, Delete Message, etc). The current work around to this is to use the traditional
        /// OAuth authentication methods.
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="scopes"></param>
        /// <param name="codeCallback"></param>
        /// <returns></returns>
        public static async Task<MixerConnection> ConnectViaShortCode(string clientID, string clientSecret, IEnumerable<OAuthClientScopeEnum> scopes, Action<OAuthShortCodeModel> codeCallback)
        {
            Validator.ValidateString(clientID, "clientID");
            Validator.ValidateList(scopes, "scopes");
            Validator.ValidateVariable(codeCallback, "codeCallback");

            OAuthService oauthService = new OAuthService();
            OAuthShortCodeModel shortCode = await oauthService.GetShortCode(clientID, clientSecret, scopes);

            codeCallback(shortCode);

            string authorizationCode = null;
            for (int i = 0; i < shortCode.expires_in && string.IsNullOrEmpty(authorizationCode); i++)
            {
                await Task.Delay(500);
                authorizationCode = await oauthService.ValidateShortCode(shortCode);
            }

            if (!string.IsNullOrEmpty(authorizationCode))
            {
                return await MixerConnection.ConnectViaAuthorizationCode(clientID, authorizationCode);
            }
            return null;
        }

        public static async Task<string> GetAuthorizationCodeURLForOAuthBrowser(string clientID, IEnumerable<OAuthClientScopeEnum> scopes, string redirectUri)
        {
            return await MixerConnection.GetAuthorizationCodeURLForOAuthBrowser(clientID, null, scopes, redirectUri);
        }

        public static async Task<string> GetAuthorizationCodeURLForOAuthBrowser(string clientID, string clientSecret, IEnumerable<OAuthClientScopeEnum> scopes, string redirectUri)
        {
            Validator.ValidateString(clientID, "clientID");
            Validator.ValidateList(scopes, "scopes");
            Validator.ValidateString(redirectUri, "redirectUri");

            string url = "https://mixer.com/oauth/authorize";

            Dictionary<string, string> parameters = new Dictionary<string, string>()
            {
                { "client_id", clientID },
                { "scope", MixerConnection.ConvertClientScopesToString(scopes) },
                { "response_type", "code" },
                { "redirect_uri", redirectUri },
            };

            if (!string.IsNullOrEmpty(clientSecret))
            {
                parameters.Add("client_secret", clientSecret);
            }

            FormUrlEncodedContent content = new FormUrlEncodedContent(parameters.AsEnumerable());

            return url + "?" + await content.ReadAsStringAsync();
        }

        public static async Task<MixerConnection> ConnectViaLocalhostOAuthBrowser(string clientID, IEnumerable<OAuthClientScopeEnum> scopes)
        {
            return await ConnectViaLocalhostOAuthBrowser(clientID, null, scopes);
        }

        public static async Task<MixerConnection> ConnectViaLocalhostOAuthBrowser(string clientID, string clientSecret, IEnumerable<OAuthClientScopeEnum> scopes)
        {
            Validator.ValidateString(clientID, "clientID");
            Validator.ValidateList(scopes, "scopes");

            OAuthHttpListenerServer oauthServer = new OAuthHttpListenerServer(OAUTH_LOCALHOST_URL);
            oauthServer.Start();

            string url = await MixerConnection.GetAuthorizationCodeURLForOAuthBrowser(clientID, scopes, OAUTH_LOCALHOST_URL);

            Process.Start(url);

            string authorizationCode = await oauthServer.WaitForAuthorizationCode();
            oauthServer.End();

            if (authorizationCode != null)
            {
                return await MixerConnection.ConnectViaAuthorizationCode(clientID, authorizationCode, redirectUrl: OAUTH_LOCALHOST_URL);
            }
            return null;
        }

        public static async Task<MixerConnection> ConnectViaAuthorizationCode(string clientID, string authorizationCode, string redirectUrl = null)
        {
            Validator.ValidateString(clientID, "clientID");
            Validator.ValidateString(authorizationCode, "authorizationCode");

            OAuthService oauthService = new OAuthService();
            OAuthTokenModel token = await oauthService.GetOAuthTokenModel(clientID, authorizationCode, redirectUrl);
            if (token == null)
            {
                throw new InvalidOperationException("OAuth token was not acquired");
            }
            return new MixerConnection(token);
        }

        public static MixerConnection ConnectViaOAuthToken(OAuthTokenModel token)
        {
            Validator.ValidateVariable(token, "token");

            MixerConnection connection = new MixerConnection(token);

            return connection;
        }

        internal static string ConvertClientScopesToString(IEnumerable<OAuthClientScopeEnum> scopes)
        {
            string result = "";

            foreach (string scopeName in EnumHelper.GetEnumNames(scopes))
            {
                result += scopeName.Replace("__", ":") + " ";
            }

            if (result.Length > 0)
            {
                result = result.Substring(0, result.Length - 1);
            }

            return result;
        }

        private MixerConnection(OAuthTokenModel token)
        {
            Validator.ValidateVariable(token, "token");

            this.token = token;

            this.Channels = new ChannelsService(this);
            this.Chats = new ChatsService(this);
            this.Costream = new CostreamService(this);
            this.Interactive = new InteractiveService(this);
            this.OAuth = new OAuthService(this);
            this.Teams = new TeamsService(this);
            this.GameTypes = new GameTypesService(this);
            this.Users = new UsersService(this);
        }

        public async Task RefreshOAuthToken()
        {
            this.token = await this.OAuth.RefreshToken(this.token);
        }

        public OAuthTokenModel GetOAuthTokenCopy()
        {
            return new OAuthTokenModel()
            {
                clientID = this.token.clientID,
                authorizationCode = this.token.authorizationCode,
                refreshToken = this.token.refreshToken,
                accessToken = this.token.accessToken,
                expiresIn = this.token.expiresIn
            };
        }

        internal async Task<OAuthTokenModel> GetOAuthToken()
        {
            if (this.token.Expiration < DateTimeOffset.Now)
            {
                await this.RefreshOAuthToken();
            }
            return this.token;
        }
    }
}
