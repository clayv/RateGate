# RateGate
### This is Jack Leitch's RateGate code with a couple improvements specifically around thread saftety and _long_ interval times (e.g. > 24.9 days)

First, this is is essentially Jack Leitch's (http://www.jackleitch.net) RateGate code (see http://www.jackleitch.net/2010/10/better-rate-limiting-with-dot-net)

The improvements I made fix an issue in the original code base (http://www.jackleitch.com/wp-content/uploads/2010/10/RateLimiting.zip) where a check is performed that the interval requested in the timeUnit parameter of the constructor is less than **UInt32**.MaxValue, but a couple lines after that the timeUint parameter is cast as an **int**. If the value passed was greater than Int32.MaxValue but less than UInt32.MaxValue, it would successfully pass error checking, but then the TimeUnitMilliseconds property would be set to a negative value.

The other improvement was around thread safety and the use of a lock which allows it to pass the testing console app in a StackOverflow post (https://stackoverflow.com/questions/10526760/how-to-call-semaphoreslim-release-without-risking-app-failure).
