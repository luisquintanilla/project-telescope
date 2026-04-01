// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using Microsoft.Extensions.AI;

namespace Telescope.Extensions.AI.ChatCompletion;

/// <summary>
/// Extension methods for adding Project Telescope observability to an M.E.AI chat client pipeline.
/// </summary>
public static class ChatClientBuilderExtensions
{
    /// <summary>
    /// Adds Project Telescope observability to the chat client pipeline.
    /// </summary>
    public static ChatClientBuilder UseTelescope(
        this ChatClientBuilder builder,
        Action<TelescopeChatClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new TelescopeChatClientOptions { AgentId = "", AgentName = "" };
        configure(options);
        return builder.Use(inner => new TelescopeChatClient(inner, options));
    }

    /// <summary>
    /// Adds Project Telescope observability with pre-configured options.
    /// </summary>
    public static ChatClientBuilder UseTelescope(
        this ChatClientBuilder builder,
        TelescopeChatClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        return builder.Use(inner => new TelescopeChatClient(inner, options));
    }
}
