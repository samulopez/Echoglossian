﻿// <copyright file="UiChatBubbleHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private void ChatBubblesOnChatBubble(ref GameObject gameObject, ref SeString text)
    {
      PluginLog.Warning($"Chat Bubble text: {text.TextValue}");
    }
  }
}
