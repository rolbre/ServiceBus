	interface commandContext {
		iD: string;
		commandMode: string;
		commandText: string;
		senderUserID: string;
		senderConnectionID: string;
		targetDeviceIDs: string[];
		pSCoreResults: any[];
		commandResults: any[];
		timeStamp: Date;
		organization: {
			iD: string;
			organizationName: string;
			remotelyUsers: any[];
			devices: any[];
			commandContexts: .commandContext[];
			eventLogs: any[];
			permissionGroups: any[];
			inviteLinks: any[];
			sharedFiles: any[];
		};
		organizationID: string;
	}