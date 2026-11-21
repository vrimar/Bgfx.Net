using Foundation;
using UIKit;

namespace Bgfx.Net.IosSmoke;

[Register("AppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
    public override bool FinishedLaunching(
        UIApplication application,
        NSDictionary? launchOptions
    ) => true;

    public override UISceneConfiguration GetConfiguration(
        UIApplication application,
        UISceneSession connectingSceneSession,
        UISceneConnectionOptions options
    ) => UISceneConfiguration.Create("Default Configuration", connectingSceneSession.Role);
}

[Register("SceneDelegate")]
public sealed class SceneDelegate : UIResponder, IUIWindowSceneDelegate
{
    [Export("window")]
    public UIWindow? Window { get; set; }

    [Export("scene:willConnectToSession:options:")]
    public void WillConnect(
        UIScene scene,
        UISceneSession session,
        UISceneConnectionOptions connectionOptions
    )
    {
        if (scene is not UIWindowScene windowScene)
        {
            return;
        }

        var controller = new UIViewController();
        Window = new UIWindow(windowScene) { RootViewController = controller };
        Window.MakeKeyAndVisible();
        BeginInvokeOnMainThread(() => Smoke.Run(controller.View!));
    }
}
