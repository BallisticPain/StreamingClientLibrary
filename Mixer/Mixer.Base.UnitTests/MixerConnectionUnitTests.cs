﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mixer.Base.Model.Channel;
using StreamingClient.Base.Model.OAuth;
using StreamingClient.Base.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Mixer.Base.UnitTests
{
    [TestClass]
    public class MixerConnectionUnitTests : UnitTestBase
    {
        [TestMethod]
        public void AuthorizeViaShortCode()
        {
            try
            {
                MixerConnection connection = MixerConnection.ConnectViaShortCode(clientID, new List<OAuthClientScopeEnum>() { OAuthClientScopeEnum.chat__connect },
                (OAuthShortCodeModel code) =>
                {
                    Assert.IsNotNull(code);

                    ProcessStartInfo startInfo = new ProcessStartInfo() { FileName = "https://www.mixer.com/oauth/shortcode?code=" + code.code, UseShellExecute = true };
                    Process.Start(startInfo);
                }).Result;

                Assert.IsNotNull(connection);
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.ToString());
            }
        }

        [TestMethod]
        public void AuthorizeViaOAuthRedirect()
        {
            TestWrapper((MixerConnection connection) =>
            {
                return Task.FromResult(0);
            });
        }

        [TestMethod]
        public void GetAuthorizationCodeURLForOAuth()
        {
            string url = MixerConnection.GetAuthorizationCodeURLForOAuthBrowser(clientID, scopes, MixerConnection.DEFAULT_OAUTH_LOCALHOST_URL).Result;

            Assert.IsNotNull(url);
        }

        [TestMethod]
        public void RefreshToken()
        {
            TestWrapper(async (MixerConnection connection) =>
            {
                OAuthTokenModel token = connection.GetOAuthTokenCopy();
                Assert.IsNotNull(token);

                ChannelModel channel = await connection.Channels.GetChannel("ChannelOne");
                Assert.IsNotNull(channel);

                await connection.RefreshOAuthToken();
                token = connection.GetOAuthTokenCopy();
                Assert.IsNotNull(token);

                channel = await connection.Channels.GetChannel("ChannelOne");
                Assert.IsNotNull(channel);
            });
        }

        [TestMethod]
        public void ConnectWithOldOAuthToken()
        {
            TestWrapper(async (MixerConnection connection) =>
            {
                MixerConnection connection2 = await MixerConnection.ConnectViaOAuthToken(connection.GetOAuthTokenCopy());
                ChannelModel channel = await connection2.Channels.GetChannel("ChannelOne");

                MixerConnection connection3 = await MixerConnection.ConnectViaOAuthToken(connection2.GetOAuthTokenCopy());
                channel = await connection3.Channels.GetChannel("ChannelOne");
            });
        }
    }
}
