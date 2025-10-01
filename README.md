# ExactlyOnce.NServiceBus.TransactionalOutbox

This repository contains an proof of concept implementation for the idea of atomic outbox.

## How it works

The implementation uses [Harmony](https://github.com/pardeike/Harmony) ([here](https://github.com/exactly-once/ExactlyOnce.NServiceBus.TransactionalOutbox/blob/master/AtomicConsumeDispatchOutbox.Sql/OutboxPatcher.cs)) to allow NServiceBus Outbox feature to be anabled when the transport is set to `SendsAtomicWithReceive` transaction mode. 

The approach moves the point when the outbox record is marked as dispatched to processing of a follow-up control message, itself part of the outbox batch. This ensures that no duplicates are ever introduced in the system (as it is running in the `SendsAtomicWithReceive` mode).

Moreover, a `Renew-Lock` API call placed between inserting a record that represents and attempt at processing a message and updating the same record ensure that the thread that commits the outbox transaction **is the only one that can successfully transition the message to the processed state**. This adjustment means that the marking as dispatched can be replaced by simple **immediate removal of the outbox record**. 

As a result, the outbox in this mode does not accumulate any data beyound the scope of the outbox transaction.
