#import "../packaging/updater-bridge.m"

static void Require(BOOL condition, NSString *message)
{
    if (!condition) {
        NSLog(@"%@", message);
        exit(1);
    }
}

int main(void)
{
    @autoreleasepool {
        NSString *suite = [@"de.ltsoftware.macstorageatlas.bridge-tests." stringByAppendingString:NSUUID.UUID.UUIDString];
        NSUserDefaults *defaults = [[NSUserDefaults alloc] initWithSuiteName:suite];
        Require(MSAConsumeIncompleteInstallation(defaults, @"1.0.0") == nil, @"Fresh installs must not report failure.");
        [defaults setObject:@"1.0.1" forKey:MSAApprovedInstallationVersionKey];
        Require([MSAConsumeIncompleteInstallation(defaults, @"1.0.0") isEqualToString:@"1.0.1"], @"The unchanged old version must report an incomplete approved installation.");
        Require(MSAConsumeIncompleteInstallation(defaults, @"1.0.0") == nil, @"Recovery must be reported only once.");
        [defaults setObject:@"1.0.1" forKey:MSAApprovedInstallationVersionKey];
        Require(MSAConsumeIncompleteInstallation(defaults, @"1.0.1") == nil, @"Successful replacement must not report failure.");
        [defaults setObject:@"1.0.1" forKey:MSAApprovedInstallationVersionKey];
        Require(MSAConsumeIncompleteInstallation(defaults, @"1.0.2") == nil, @"A newer manual installation must not report failure.");
        [defaults setObject:@"1.0.10" forKey:MSAApprovedInstallationVersionKey];
        Require(MSAConsumeIncompleteInstallation(defaults, @"1.0.9") != nil, @"Version comparison must compare numeric components.");
        [defaults setObject:@"1.0.1" forKey:MSAApprovedInstallationVersionKey];
        Require(MSAConsumeIncompleteInstallation(defaults, nil) == nil, @"Missing bundle metadata must not manufacture an installation failure.");
        [defaults removePersistentDomainForName:suite];
        puts("7 native updater recovery tests passed");
    }
    return 0;
}
