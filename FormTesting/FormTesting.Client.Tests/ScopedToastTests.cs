using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace FormTesting.Client.Tests;

/// <summary>
/// Tests for the scoped (Server-safe) toast variant: the DI-registered IMessageService /
/// INotificationService + their MessageContainer / NotificationContainer hosts. Unlike the static
/// Wasm* services, state lives on the instance, so it does not bleed across users/circuits.
/// </summary>
public class ScopedToastTests : BunitContext
{
    [Fact]
    public void MessageContainer_renders_messages_from_injected_service()
    {
        Services.AddWssControlsToasts();
        var svc = Services.GetRequiredService<IMessageService>();
        svc.Success("Scoped saved", duration: 0);

        var cut = Render<MessageContainer>();

        Assert.Contains("Scoped saved", cut.Find(".wss-msg-content").TextContent);
        Assert.NotNull(cut.Find(".wss-msg-icon-success"));
    }

    [Fact]
    public void NotificationContainer_renders_then_close_removes()
    {
        Services.AddWssControlsToasts();
        var svc = Services.GetRequiredService<INotificationService>();
        svc.Info("Scoped notice", "the details", duration: 0);

        var cut = Render<NotificationContainer>();

        Assert.Contains("Scoped notice", cut.Find(".wss-notification-message").TextContent);
        Assert.Contains("the details", cut.Find(".wss-notification-description").TextContent);

        cut.Find(".wss-notification-close").Click();
        Assert.Empty(cut.FindAll(".wss-notification"));
    }

    [Fact]
    public void NotificationContainer_forwards_Placement_to_the_shared_list_view()
    {
        Services.AddWssControlsToasts();
        Services.GetRequiredService<INotificationService>().Info("x", duration: 0);

        var cut = Render<NotificationContainer>(p => p.Add(c => c.Placement, NotificationPlacement.BottomLeft));

        Assert.Contains("wss-notification-bottomleft", cut.Find(".wss-notification-container").ClassList);
    }

    [Fact]
    public async Task Disposed_MessageContainer_unsubscribes_from_the_service()
    {
        Services.AddWssControlsToasts();
        var svc = Services.GetRequiredService<IMessageService>();
        var cut = Render<MessageContainer>();

        await DisposeComponentsAsync();

        // If the container leaked its OnChange subscription, this would StateHasChanged a
        // disposed component and throw.
        svc.Success("after dispose", duration: 0);
        Assert.Single(svc.Items);
    }

    [Fact]
    public void Two_message_service_instances_do_not_share_state()
    {
        // The whole point of the scoped variant vs. the static WasmMessageService: independent
        // instances (e.g. different Server circuits) keep separate state.
        var a = new MessageService();
        var b = new MessageService();

        a.Success("only in a", duration: 0);

        Assert.Single(a.Items);
        Assert.Empty(b.Items);
    }

    [Fact]
    public void Loading_returns_an_id_that_dismisses_the_sticky_toast()
    {
        // A loading toast is sticky (duration 0) and has no close button, so the only way to
        // dismiss just it (rather than Clear()-ing everything) is the id returned from Loading.
        var svc = new MessageService();
        svc.Success("other", duration: 0);
        var loadingId = svc.Loading("Saving...");

        Assert.Equal(2, svc.Items.Count);

        svc.Remove(loadingId);

        Assert.Single(svc.Items);                                 // only the loading toast went away
        Assert.DoesNotContain(svc.Items, m => m.Id == loadingId);
        Assert.Contains(svc.Items, m => m.Content == "other");    // the other toast survived
    }

    [Fact]
    public void Notification_add_returns_an_id_that_dismisses_that_one_notification()
    {
        // The four add methods used to return void, which made INotificationService.Remove(Guid)
        // unreachable for a consumer (nothing handed out an id) -- Clear() was the only programmatic
        // dismissal, and it took every notification with it. Sticky notifications (duration 0) are the
        // case that needs one specific id.
        var svc = new NotificationService();
        svc.Warning("stays put", duration: 0);
        var id = svc.Error("dismiss me", "with a description", duration: 0);

        Assert.Equal(2, svc.Items.Count);
        Assert.Contains(svc.Items, n => n.Id == id);

        svc.Remove(id);

        Assert.Single(svc.Items);
        Assert.DoesNotContain(svc.Items, n => n.Id == id);
        Assert.Contains(svc.Items, n => n.Message == "stays put");
    }

    [Fact]
    public void Notification_ids_are_distinct_across_the_four_severities()
    {
        // One id per notification, so a caller holding several can dismiss exactly the one it means.
        var svc = new NotificationService();
        Guid[] ids = [svc.Success("a", duration: 0), svc.Info("b", duration: 0),
                      svc.Warning("c", duration: 0), svc.Error("d", duration: 0)];

        Assert.Equal(4, ids.Distinct().Count());
        Assert.Equal(ids, svc.Items.Select(n => n.Id));
    }

    [Fact]
    public void AddWssControlsToasts_registers_both_services_as_scoped()
    {
        var services = new ServiceCollection();
        services.AddWssControlsToasts();

        Assert.Contains(services, d => d.ServiceType == typeof(IMessageService) && d.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, d => d.ServiceType == typeof(INotificationService) && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void Error_message_goes_to_an_assertive_alert_region()
    {
        Services.AddWssControlsToasts();
        Services.GetRequiredService<IMessageService>().Error("boom", duration: 0);

        var cut = Render<MessageContainer>();
        // The error renders inside the always-present assertive region...
        var assertive = cut.Find(".wss-msg-region[role=alert]");
        Assert.Equal("assertive", assertive.GetAttribute("aria-live"));
        Assert.Contains("boom", assertive.TextContent);
        // ...while the polite region stays present (so it's ready for later polite toasts) but empty.
        var polite = cut.Find(".wss-msg-region[role=status]");
        Assert.Equal("polite", polite.GetAttribute("aria-live"));
        Assert.Empty(polite.QuerySelectorAll(".wss-msg"));
    }

    [Fact]
    public void Non_error_message_goes_to_a_polite_status_region()
    {
        Services.AddWssControlsToasts();
        Services.GetRequiredService<IMessageService>().Info("hi", duration: 0);

        var cut = Render<MessageContainer>();
        var polite = cut.Find(".wss-msg-region[role=status]");
        Assert.Equal("polite", polite.GetAttribute("aria-live"));
        Assert.Contains("hi", polite.TextContent);
        // No spurious alert: the assertive region is present but empty.
        Assert.Empty(cut.Find(".wss-msg-region[role=alert]").QuerySelectorAll(".wss-msg"));
    }

    [Fact]
    public void Loading_with_no_duration_stays_sticky()
    {
        var svc = new MessageService();
        svc.Loading("working");   // duration defaults to 0 -> no auto-dismiss timer
        Assert.Single(svc.Items);
    }

    [Fact]
    public async Task Message_auto_removes_after_its_duration()
    {
        var svc = new MessageService();
        svc.Success("bye", duration: 0.05);   // 50ms
        Assert.Single(svc.Items);

        // Poll to a deadline instead of racing one fixed wall-clock wait against the real
        // auto-dismiss timer. RemoveAfterAsync's post-Task.Delay continuation resumes on the
        // threadpool, which can be starved under CI load, so a single Task.Delay(400) was
        // intermittently too short. A bounded poll passes on a slow box without making a fast one wait.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (svc.Items.Count > 0 && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.Empty(svc.Items);
    }

    [Fact]
    public async Task Clear_cancels_pending_auto_dismiss()
    {
        var svc = new MessageService();
        svc.Success("x", duration: 0.05);
        svc.Clear();
        Assert.Empty(svc.Items);

        // The cancelled timer must not throw or resurrect/double-remove later.
        await Task.Delay(250);
        Assert.Empty(svc.Items);
    }

    [Fact]
    public void Concurrent_Clear_calls_never_cancel_an_already_disposed_timer()
    {
        // CancelAllTimers used to walk a _timers.Values snapshot and Cancel()+Dispose() every source
        // without claiming it, so two callers reaching the same CancellationTokenSource both disposed
        // it and the loser threw ObjectDisposedException out of the user's own Clear() (a circuit
        // error on Blazor Server). The real-world pair is a threadpool auto-dismiss expiry racing a
        // Clear(); two racing Clear()s exercise the identical unclaimed-entry window deterministically,
        // with no dependence on timer wall-clock. With the TryRemove claim, exactly one caller ever
        // owns an entry, so this can only pass.
        var queue = new ToastQueue<MessageItem>();
        for (var i = 0; i < 500; i++)
            queue.Add(new MessageItem { Content = $"m{i}", Duration = 60 }); // long enough not to expire mid-test

        Exception? failure = null;
        using var start = new ManualResetEventSlim();
        var threads = Enumerable.Range(0, 2).Select(_ => new Thread(() =>
        {
            start.Wait();
            try { queue.Clear(); }
            catch (Exception ex) { Interlocked.CompareExchange(ref failure, ex, null); }
        })).ToList();

        foreach (var t in threads) t.Start();
        start.Set();
        foreach (var t in threads) t.Join();

        Assert.Null(failure);
        Assert.Empty(queue.Items);
    }

    // ---- S7: pause auto-dismiss on hover/focus (ToastQueue.Pause/Resume) ----

    [Fact]
    public async Task Pause_cancels_the_pending_auto_dismiss_timer()
    {
        var svc = new MessageService();
        var id = svc.Success("hover me", duration: 0.05); // 50ms

        svc.Pause(id);
        await Task.Delay(250); // well past the original duration -- Pause must have cancelled it

        Assert.Single(svc.Items);
    }

    [Fact]
    public async Task Resume_restarts_a_fresh_full_duration_timer_rather_than_a_remaining_one()
    {
        var svc = new MessageService();
        var id = svc.Success("hover then leave", duration: 0.2); // 200ms

        svc.Pause(id);
        await Task.Delay(500); // if Pause hadn't truly cancelled the timer, it would have fired by now
        Assert.Single(svc.Items);

        svc.Resume(id);

        // Immediately after Resume the fresh 200ms countdown has barely started. If Resume instead
        // resumed a "time remaining" clock (already fully consumed by the 500ms Pause window above),
        // the item would already be gone.
        Assert.Single(svc.Items);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (svc.Items.Count > 0 && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.Empty(svc.Items); // the resumed timer eventually fires on its own
    }

    [Fact]
    public void Pause_and_Resume_are_no_ops_on_a_sticky_item()
    {
        var svc = new MessageService();
        var id = svc.Loading("working"); // duration 0 -- sticky, no timer to begin with

        svc.Pause(id);  // must not throw, must not remove the item
        svc.Resume(id); // must not throw, must not start an unwanted timer

        Assert.Single(svc.Items);
    }

    [Fact]
    public void Pause_and_Resume_are_no_ops_on_an_unknown_id()
    {
        var svc = new MessageService();
        svc.Pause(Guid.NewGuid());
        svc.Resume(Guid.NewGuid());

        Assert.Empty(svc.Items);
    }

    [Fact]
    public async Task Resume_on_an_item_that_was_never_paused_just_restarts_its_timer()
    {
        // Mirrors mouseleave and focusout both firing for the same toast as the pointer and
        // keyboard focus leave together (see Resume's own doc comment) -- calling Resume without a
        // prior Pause is not an error.
        var svc = new MessageService();
        svc.Success("still ticking", duration: 0.05);

        svc.Resume(svc.Items[0].Id); // no Pause first

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (svc.Items.Count > 0 && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.Empty(svc.Items);
    }

    [Fact]
    public void Concurrent_Pause_and_Resume_calls_on_the_same_id_never_throw()
    {
        // Pause and Resume both ultimately touch the same _timers slot the auto-dismiss timer's own
        // expiry touches -- exercise many racing Pause/Resume pairs on one id and confirm none of
        // them throw. ObjectDisposedException was the historical failure mode for exactly this kind
        // of shared-slot race; see CancelAllTimers's own comment in ToastQueue.
        var queue = new ToastQueue<MessageItem>();
        var item = new MessageItem { Content = "x", Duration = 60 }; // long enough not to expire mid-test
        queue.Add(item);

        Exception? failure = null;
        using var start = new ManualResetEventSlim();
        var threads = Enumerable.Range(0, 4).Select(i => new Thread(() =>
        {
            start.Wait();
            try
            {
                for (var j = 0; j < 100; j++)
                {
                    if (i % 2 == 0) queue.Pause(item.Id);
                    else queue.Resume(item.Id);
                }
            }
            catch (Exception ex) { Interlocked.CompareExchange(ref failure, ex, null); }
        })).ToList();

        foreach (var t in threads) t.Start();
        start.Set();
        foreach (var t in threads) t.Join();

        Assert.Null(failure);
    }

    [Fact]
    public async Task Pause_on_one_toast_does_not_cancel_a_second_concurrent_toasts_timer()
    {
        // Per-toast isolation: ToastQueue keys _timers by item id, so pausing one toast must never
        // affect a different toast's own countdown running at the same time.
        var svc = new MessageService();
        var pausedId = svc.Success("paused", duration: 0.05);          // 50ms
        var tickingId = svc.Success("still ticking", duration: 0.05);  // 50ms, its own independent timer

        svc.Pause(pausedId);

        // The ticking toast's timer must still fire on its own schedule, untouched by the other's Pause.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (svc.Items.Any(m => m.Id == tickingId) && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.DoesNotContain(svc.Items, m => m.Id == tickingId);  // removed on its own timer
        Assert.Contains(svc.Items, m => m.Id == pausedId);         // the paused one is still here

        // Well past its original duration, the paused toast must still be present -- rules out the
        // other direction too (the ticking toast's expiry cancelling the paused one's already-paused
        // slot, or a shared-timer mixup resurrecting it).
        await Task.Delay(250);
        Assert.Contains(svc.Items, m => m.Id == pausedId);
    }

    // ---- S7: MessageListView/NotificationListView hover+focus wiring ----

    [Fact]
    public void MessageListView_hover_and_focus_invoke_OnPause_and_OnResume()
    {
        var item = new MessageItem { Content = "hover me", Duration = 0 };
        Guid? paused = null;
        Guid? resumed = null;

        var cut = Render<MessageListView>(p => p
            .Add(c => c.Items, new[] { item })
            .Add(c => c.OnPause, EventCallback.Factory.Create<Guid>(this, id => paused = id))
            .Add(c => c.OnResume, EventCallback.Factory.Create<Guid>(this, id => resumed = id)));

        var toast = cut.Find(".wss-msg");

        toast.MouseEnter();
        Assert.Equal(item.Id, paused);

        toast.MouseLeave();
        Assert.Equal(item.Id, resumed);

        paused = null;
        toast.FocusIn();
        Assert.Equal(item.Id, paused);

        resumed = null;
        toast.FocusOut();
        Assert.Equal(item.Id, resumed);
    }

    [Fact]
    public void NotificationListView_hover_and_focus_invoke_OnPause_and_OnResume()
    {
        var item = new NotificationItem { Message = "hover me", Duration = 0 };
        Guid? paused = null;
        Guid? resumed = null;

        var cut = Render<NotificationListView>(p => p
            .Add(c => c.Items, new[] { item })
            .Add(c => c.OnPause, EventCallback.Factory.Create<Guid>(this, id => paused = id))
            .Add(c => c.OnResume, EventCallback.Factory.Create<Guid>(this, id => resumed = id)));

        var toast = cut.Find(".wss-notification");

        toast.MouseEnter();
        Assert.Equal(item.Id, paused);

        toast.MouseLeave();
        Assert.Equal(item.Id, resumed);

        paused = null;
        toast.FocusIn();
        Assert.Equal(item.Id, paused);

        resumed = null;
        toast.FocusOut();
        Assert.Equal(item.Id, resumed);
    }

    [Fact]
    public async Task MessageContainer_hover_pauses_and_leave_resumes_the_services_timer()
    {
        // End-to-end: container -> IMessageService -> ToastQueue, not just the list view's callback.
        Services.AddWssControlsToasts();
        var svc = Services.GetRequiredService<IMessageService>();
        svc.Success("hover me", duration: 0.05); // 50ms

        var cut = Render<MessageContainer>();
        var toast = cut.Find(".wss-msg");

        toast.MouseEnter();
        await Task.Delay(250); // well past the original duration -- the hover pause must hold it
        Assert.Single(svc.Items);

        toast.MouseLeave();

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (svc.Items.Count > 0 && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.Empty(svc.Items); // the resumed timer eventually removes it
    }

    [Fact]
    public async Task NotificationContainer_focus_pauses_and_blur_resumes_the_services_timer()
    {
        Services.AddWssControlsToasts();
        var svc = Services.GetRequiredService<INotificationService>();
        svc.Info("focus me", duration: 0.05); // 50ms

        var cut = Render<NotificationContainer>();
        var toast = cut.Find(".wss-notification");

        toast.FocusIn();
        await Task.Delay(250);
        Assert.Single(svc.Items);

        toast.FocusOut();

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (svc.Items.Count > 0 && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.Empty(svc.Items);
    }

    // ---- M1: message close button ----

    [Fact]
    public void MessageListView_close_button_has_an_aria_label_and_invokes_OnRemove()
    {
        var item = new MessageItem { Content = "dismiss me", Duration = 0 };
        Guid? removed = null;

        var cut = Render<MessageListView>(p => p
            .Add(c => c.Items, new[] { item })
            .Add(c => c.OnRemove, EventCallback.Factory.Create<Guid>(this, id => removed = id)));

        var close = cut.Find(".wss-msg-close");
        Assert.Equal("Close", close.GetAttribute("aria-label"));

        close.Click();
        Assert.Equal(item.Id, removed);
    }

    [Fact]
    public void MessageContainer_close_button_removes_the_message_via_the_service()
    {
        Services.AddWssControlsToasts();
        var svc = Services.GetRequiredService<IMessageService>();
        svc.Success("dismiss me", duration: 0);

        var cut = Render<MessageContainer>();
        cut.Find(".wss-msg-close").Click();

        Assert.Empty(cut.FindAll(".wss-msg"));
        Assert.Empty(svc.Items);
    }

    [Fact]
    public void A_sticky_loading_message_still_gets_a_close_button()
    {
        // Previously a Loading message (duration 0, the default for Loading) had no way for the user
        // to dismiss it themselves -- only the id returned from Loading() could. The close button
        // now renders unconditionally, same as NotificationListView's.
        Services.AddWssControlsToasts();
        Services.GetRequiredService<IMessageService>().Loading("Saving...");

        var cut = Render<MessageContainer>();
        Assert.NotNull(cut.Find(".wss-msg-close"));
    }

    // ---- M2: severity announced to assistive tech ----

    [Theory]
    [InlineData(MessageType.Success, "Success: ")]
    [InlineData(MessageType.Info, "Info: ")]
    [InlineData(MessageType.Warning, "Warning: ")]
    [InlineData(MessageType.Error, "Error: ")]
    [InlineData(MessageType.Loading, "Loading: ")]
    public void MessageListView_renders_a_sr_only_severity_word_before_the_content(MessageType type, string expectedLabel)
    {
        var item = new MessageItem { Type = type, Content = "hi", Duration = 0 };
        var cut = Render<MessageListView>(p => p.Add(c => c.Items, new[] { item }));

        Assert.Equal(expectedLabel, cut.Find(".wss-msg .wss-sr-only").TextContent);
    }

    [Theory]
    [InlineData(NotificationType.Success, "Success: ")]
    [InlineData(NotificationType.Info, "Info: ")]
    [InlineData(NotificationType.Warning, "Warning: ")]
    [InlineData(NotificationType.Error, "Error: ")]
    public void NotificationListView_renders_a_sr_only_severity_word_before_the_message(NotificationType type, string expectedLabel)
    {
        var item = new NotificationItem { Type = type, Message = "hi", Duration = 0 };
        var cut = Render<NotificationListView>(p => p.Add(c => c.Items, new[] { item }));

        Assert.Equal(expectedLabel, cut.Find(".wss-notification .wss-sr-only").TextContent);
    }

    // ---- Localizable CloseButtonLabel / SeverityLabel overrides on the containers ----

    [Fact]
    public void MessageContainer_forwards_CloseButtonLabel_and_SeverityLabel_overrides_to_the_shared_list_view()
    {
        Services.AddWssControlsToasts();
        Services.GetRequiredService<IMessageService>().Warning("x", duration: 0);

        var cut = Render<MessageContainer>(p => p
            .Add(c => c.CloseButtonLabel, "Dismiss")
            .Add(c => c.SeverityLabel, (Func<MessageType, string>)(_ => "Attention")));

        Assert.Equal("Dismiss", cut.Find(".wss-msg-close").GetAttribute("aria-label"));
        Assert.Equal("Attention: ", cut.Find(".wss-msg .wss-sr-only").TextContent);
    }

    [Fact]
    public void MessageContainer_defaults_are_unchanged_without_the_new_overrides()
    {
        Services.AddWssControlsToasts();
        Services.GetRequiredService<IMessageService>().Info("x", duration: 0);

        var cut = Render<MessageContainer>();

        Assert.Equal("Close", cut.Find(".wss-msg-close").GetAttribute("aria-label"));
        Assert.Equal("Info: ", cut.Find(".wss-msg .wss-sr-only").TextContent);
    }

    [Fact]
    public void NotificationContainer_forwards_CloseButtonLabel_and_SeverityLabel_overrides_to_the_shared_list_view()
    {
        Services.AddWssControlsToasts();
        Services.GetRequiredService<INotificationService>().Error("x", duration: 0);

        var cut = Render<NotificationContainer>(p => p
            .Add(c => c.CloseButtonLabel, "Dismiss")
            .Add(c => c.SeverityLabel, (Func<NotificationType, string>)(_ => "Urgent")));

        Assert.Equal("Dismiss", cut.Find(".wss-notification-close").GetAttribute("aria-label"));
        Assert.Equal("Urgent: ", cut.Find(".wss-notification .wss-sr-only").TextContent);
    }

    [Fact]
    public void NotificationContainer_defaults_are_unchanged_without_the_new_overrides()
    {
        Services.AddWssControlsToasts();
        Services.GetRequiredService<INotificationService>().Info("x", duration: 0);

        var cut = Render<NotificationContainer>();

        Assert.Equal("Close", cut.Find(".wss-notification-close").GetAttribute("aria-label"));
        Assert.Equal("Info: ", cut.Find(".wss-notification .wss-sr-only").TextContent);
    }

    // ---- Per-toast aria-describedby (distinguishes "Close, button" across a stack) ----

    [Fact]
    public void MessageListView_close_button_aria_describedby_resolves_to_its_own_toasts_content()
    {
        var a = new MessageItem { Content = "first toast", Duration = 0 };
        var b = new MessageItem { Content = "second toast", Duration = 0 };

        var cut = Render<MessageListView>(p => p.Add(c => c.Items, new[] { a, b }));

        var closeButtons = cut.FindAll(".wss-msg-close");
        var contents = cut.FindAll(".wss-msg-content");
        Assert.Equal(2, closeButtons.Count);

        // Each close button's aria-describedby has to resolve to ITS OWN toast's content element,
        // not the other one's -- otherwise a screen reader tabbing through the stack still can't
        // tell the two "Close" buttons apart.
        for (var i = 0; i < closeButtons.Count; i++)
        {
            var describedBy = closeButtons[i].GetAttribute("aria-describedby");
            Assert.False(string.IsNullOrEmpty(describedBy));
            Assert.Equal(describedBy, contents[i].GetAttribute("id"));
        }
        Assert.NotEqual(closeButtons[0].GetAttribute("aria-describedby"), closeButtons[1].GetAttribute("aria-describedby"));
    }

    [Fact]
    public void NotificationListView_close_button_aria_describedby_resolves_to_its_own_toasts_message()
    {
        var a = new NotificationItem { Message = "first notice", Duration = 0 };
        var b = new NotificationItem { Message = "second notice", Duration = 0 };

        var cut = Render<NotificationListView>(p => p.Add(c => c.Items, new[] { a, b }));

        var closeButtons = cut.FindAll(".wss-notification-close");
        var messages = cut.FindAll(".wss-notification-message");
        Assert.Equal(2, closeButtons.Count);

        for (var i = 0; i < closeButtons.Count; i++)
        {
            var describedBy = closeButtons[i].GetAttribute("aria-describedby");
            Assert.False(string.IsNullOrEmpty(describedBy));
            Assert.Equal(describedBy, messages[i].GetAttribute("id"));
        }
        Assert.NotEqual(closeButtons[0].GetAttribute("aria-describedby"), closeButtons[1].GetAttribute("aria-describedby"));
    }
}
