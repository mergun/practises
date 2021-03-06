﻿const CHARSET			= "UTF-8";
const REPLACEMENTCHAR	= Components.interfaces.nsIConverterInputStream.DEFAULT_REPLACEMENT_CHARACTER;

var practises = {
	/*
	 * Event handlers
	 */
	onLoad: function(e) {
		this.initialized = true;
		this.strings = document.getElementById("practises-strings");
		this.messagePane = document.getElementById("messagepane");
		this.prefs = Components.classes["@mozilla.org/preferences-service;1"].getService(Components.interfaces.nsIPrefService);
		this.prefs = this.prefs.getBranch("extensions.practises.");
	},
	
	onComposerSendMessage: function(e) {
		var signRadio = document.getElementById("practises-sign");
		var encryptRadio = document.getElementById("practises-encrypt");
		var signAndEncryptRadio = document.getElementById("practises-signAndEncrypt");

		var CHECKED = signRadio.accessible.STATE_CHECKED;

		var sign = false;
		var encrypt = false;
		
		if(signAndEncryptRadio.accessible.finalState & CHECKED) {
			sign = true;
			encrypt = true;
		} else if(signRadio.accessible.finalState & CHECKED) {
			sign = true;
		} else if(encryptRadio.accessible.finalState & CHECKED) {
			encrypt = true;
		}

		if(sign) {
			practises.sign();
		}

		if(encrypt) {
			practises.encrypt();
		}
	},
	
	/*
	 * Cryptographic functions
	 */
	
	encrypt: function() {
		var recipients = gMsgCompose.compFields.to.split(",");
		var recipient = practises.stripEmail(recipients[0]);
		practises.call("-e", recipient);
		
		var attachments = gMsgCompose.compFields.attachmentsArray;
		var attachmentCount = attachments.Count();
		
		var toEncrypt = Array();
		for(var i = 0; i < attachmentCount; i++) {
			toEncrypt[i] = attachments.GetElementAt(i);
		}

		for(index in toEncrypt) {
			var attachment = toEncrypt[index];
			practises.addEncryptedAttachment(recipient, attachment.url);
			gMsgCompose.compFields.removeAttachment(attachment);
		}
	},
	
	decrypt: function(e) {
		var passphrase = practises.prompt("PractiSES", "Enter passphrase:");
		practises.call("-d", passphrase);
		var files = practises.saveViewAttachments();
		this.timeoutID = window.setTimeout(practises.decryptAttachments, 1000, this, files, passphrase);
		document.getElementById("psesBox2").collapsed = true;
	},
	
	decryptAttachments: function(that, files, passphrase) {
		var host = that.prefs.getCharPref("server");
		var returnVals = Array();
		var paths = Array();
		var args;
		
		var attachmentList = document.getElementById('attachmentList');
		while(attachmentList.childNodes.length > 0)
			attachmentList.removeChild(attachmentList.childNodes[0]);

		var count = 0;				
		for (index in files) {
			var file = files[index];
			var psesIndex = file.path.indexOf(".pses");
			if((psesIndex != -1) && (psesIndex == file.path.length - 5)) {
				var decryptedPath = file.path.substring(0, psesIndex);
				args = ["-d", "-H", host, "-p", passphrase, "-O", decryptedPath, file.path];
				practises.run(args);
				paths[count++] = decryptedPath;
			}
		}
		
		for (index in paths) {
			practises.addViewAttachment(paths[index]);
		}
		
		for (index in files) {
			if(files[index].exists())
				files[index].remove(false);
		}
	},
	
	sign: function() {
		var passphrase = practises.prompt("PractiSES", "Enter passphrase:");
		practises.call("-s", passphrase);
		
		var attachments = gMsgCompose.compFields.attachmentsArray;
		var attachmentCount = attachments.Count();
		
		for(var i = 0; i < attachmentCount; i++) {
			var attachment = attachments.GetElementAt(i);
			practises.addDetachedSignature(passphrase, attachment.url);
		}
	},
	
	verify: function(author, message) {
		var result = practises.call("-v", author, message);

		if(currentAttachments.length > 0) {
			var files = practises.saveViewAttachments();
			this.timeoutID = window.setTimeout(practises.verifyAttachments, 1000, this, files, author, result);
		} else {
			practises.setVerifyIcon(result);
		}
	},
	
	verifyAttachments: function(that, files, author, messageResult) {
		var host = that.prefs.getCharPref("server");
		var returnVals = Array();
		var paths = Array();
		var result = false;
		var args;
				
		for (index in files) {
			paths[index] = files[index].path;
		}

		var count = 0;				
		for (index in files)	{
			var file = files[index];
			var psesIndex = file.path.indexOf(".pses");
			if((psesIndex == -1) || (psesIndex != file.path.length - 5)) {
				if(paths.indexOf(file.path + ".pses") != -1) {
					args = ["--verify-detached", "-H", host, "-r", author, file.path];
					returnVals[count++] = practises.run(args);
				}
			}
		}
		
		for (index in files) {
			if(files[index].exists())
				files[index].remove(false);
		}
		
		for (index in returnVals) {
			if(returnVals[index] == 1) {
				result = false;
			}
		}
		
		result = true;
		
		if(result) {
			practises.setVerifyIcon(messageResult);
		} else {
			practises.setVerifyIcon(-1);
		}
	},
	
	/*
	 * PractiSES action finalization functions
	 */
	
	finalizeInitialize: function(e) {
		var passphrase = practises.prompt("PractiSES", "Enter passphrase:");
		var returnVal = practises.call("--finalize-initialize", passphrase);
		if(returnVal == 0) {
			alert("Public key set successfully.");
		}
	},
	
	finalizeUpdate: function(e) {
		var passphrase = practises.prompt("PractiSES", "Enter passphrase:");
		var returnVal = practises.call("--finalize-update", passphrase);
		if(returnVal == 0) {
			alert("Public key updated successfully.");
		}
	},
	
	finalizeRemove: function(e) {
		var passphrase = practises.prompt("PractiSES", "Enter passphrase:");
		var returnVal = practises.call("--finalize-remove", passphrase);
		if(returnVal == 0) {
			alert("Public key removed successfully.");
		}
	},
	
	/*
	 * Utility functions
	 */

	addDetachedSignature: function(passphrase, url) {
		var result = Array();
		var host = this.prefs.getCharPref("server");
		url = practises.URItoWindows(url);
		var filename = practises.getFilename(url);
		
		var tmp_file = Components.classes["@mozilla.org/file/directory_service;1"].getService(Components.interfaces.nsIProperties).get("TmpD", Components.interfaces.nsIFile);
		tmp_file.append(filename + ".pses");
		if(tmp_file.exists())
			tmp_file.remove(false);
		
		var args = ["--sign-detached", "-H", host, "-p", passphrase, "-O", tmp_file.path, url];
		var returnVal = practises.run(args);
		
		var protocolhandler = Components.classes["@mozilla.org/network/protocol;1?name=file"].createInstance(Components.interfaces.nsIFileProtocolHandler);			
		var attachmentURL = protocolhandler.getURLSpecFromFile(tmp_file);

		var attachment = Components.classes["@mozilla.org/messengercompose/attachment;1"].createInstance(Components.interfaces.nsIMsgAttachment);
		attachment.url = attachmentURL;

		if(attachment && attachment.url) {
			if (!attachment.name)
				attachment.name = filename + ".pses";
			
			gMsgCompose.compFields.addAttachment(attachment);
		}
  	},
	
	addViewAttachment: function(url) {
		var mimeService = Components.classes["@mozilla.org/mime;1"].getService(Components.interfaces.nsIMIMEService);
		
		var file = Components.classes["@mozilla.org/file/local;1"].createInstance(Components.interfaces.nsILocalFile);
		file.initWithPath(url);
		var contentType = mimeService.getTypeFromFile(file);
		
		var attachment = new createNewAttachmentInfo(contentType, url, practises.getFilename(url), document.getElementById("messagepane").currentURI.spec, true);
		var attachmentList = document.getElementById('attachmentList');
		var attachmentView = attachmentList.appendItem(attachment.displayName);
		attachmentView.setAttribute("class", "descriptionitem-iconic");

		if (gShowLargeAttachmentView)
		attachmentView.setAttribute("largeView", "true");

		setApplicationIconForAttachment(attachment, attachmentView, gShowLargeAttachmentView);
		attachmentView.setAttribute("tooltip", "attachmentListTooltip");
		attachmentView.setAttribute("context", "attachmentListContext");

		attachmentView.attachment = cloneAttachment(attachment);
		attachmentView.setAttribute("attachmentUrl", attachment.url);
		attachmentView.setAttribute("attachmentContentType", attachment.contentType);
		attachmentView.setAttribute("attachmentUri", attachment.uri);

		var item = attachmentList.appendChild(attachmentView);		
	},
	
	addEncryptedAttachment: function(recipient, url) {
		var host = this.prefs.getCharPref("server");
		url = practises.URItoWindows(url);
		var filename = url.substring(url.lastIndexOf("\\") + 1, url.length);
		
		var tmp_file = Components.classes["@mozilla.org/file/directory_service;1"].getService(Components.interfaces.nsIProperties).get("TmpD", Components.interfaces.nsIFile);
		tmp_file.append(filename + ".pses");
		if(tmp_file.exists())
			tmp_file.remove(false);
		
		var args = ["-e", "-H", host, "-r", recipient, "-O", tmp_file.path, url];
		var returnVal = practises.run(args);
		
		var protocolhandler = Components.classes["@mozilla.org/network/protocol;1?name=file"].createInstance(Components.interfaces.nsIFileProtocolHandler);			
		var attachmentURL = protocolhandler.getURLSpecFromFile(tmp_file);

		var attachment = Components.classes["@mozilla.org/messengercompose/attachment;1"].createInstance(Components.interfaces.nsIMsgAttachment);
		attachment.url = attachmentURL;

		if(attachment && attachment.url) {
			if (!attachment.name)
				attachment.name = filename + ".pses";
			
			gMsgCompose.compFields.addAttachment(attachment);
		}
  	},
	
	call: function(command, argument) {
		return practises.call(command, argument, null);
	},
	
	call: function(command, argument, message) {
		var host = this.prefs.getCharPref("server");
		var hasOutput = false;
		var composing = false;
		var needsRecipient = false;
		var needsPassphrase = false;
		var psesEditor = null;
		
		if(command == "-s" || command == "-d" || command == "--finalize-initialize" || command == "--finalize-update" || command == "--finalize-remove")
			needsPassphrase = true;
		
		if(command == "-s" || command == "-e" || command == "-d")
			hasOutput = true;
		
		if(command == "-s" || command == "-e") 
			composing = true;

		if(command == "-e" || command == "-v")
			needsRecipient = true;
		
		var tmp_file = Components.classes["@mozilla.org/file/directory_service;1"].getService(Components.interfaces.nsIProperties).get("TmpD", Components.interfaces.nsIFile);
		tmp_file.append("message.tmp");
		tmp_file.createUnique(Components.interfaces.nsIFile.NORMAL_FILE_TYPE, 0664);

		if(message == null) {
			if(composing) {
				psesEditor = gMsgCompose.editor.QueryInterface(Components.interfaces.nsIPlaintextEditor);
				message = psesEditor.outputToString("text/plain", 2 | 1024);
				/*
				SetDocumentCharacterSet(CHARSET);
				if(gMsgCompose.bodyConvertible() == nsIMsgCompConvertible.Plain)
					OutputFileWithPersistAPI(GetCurrentEditor().document, tmp_file, null, "text/plain");
				else
					OutputFileWithPersistAPI(GetCurrentEditor().document, tmp_file, null, "text/html");
				*/
			} else {
				message = practises.readMessage();
			}
		}
		
		var ostream = Components.classes["@mozilla.org/network/file-output-stream;1"].createInstance(Components.interfaces.nsIFileOutputStream);
		ostream.init(tmp_file, 0x02 | 0x08 | 0x20, 0664, 0);
		var os = Components.classes["@mozilla.org/intl/converter-output-stream;1"].createInstance(Components.interfaces.nsIConverterOutputStream);
		os.init(ostream, CHARSET, 1024, REPLACEMENTCHAR);
		os.writeString(message);
		os.close();
		ostream.close();
			
		var args;
		if(needsPassphrase) {
			args = [command, "-H", host, "-p", argument, tmp_file.path];
		} else if(needsRecipient) {
			args = [command, "-H", host, "-r", argument, tmp_file.path];
		}
		var returnVal = practises.run(args);

		if(hasOutput) {
			var outputPath = tmp_file.path + ".pses";
			var data = practises.readFile(outputPath);

			if(composing) {
				psesEditor.selectAll();
				psesEditor.insertText(data);
			} else {
				var msgPaneDocChildren = this.messagePane.contentDocument.childNodes;
				var ndHTML = msgPaneDocChildren.item(0);
				var ndBODY = ndHTML.childNodes.item(1);
				var ndDIV = ndBODY.childNodes.item(1);
				ndDIV.innerHTML = data.replace(/\n/g, "<br/>");
				while(ndBODY.childNodes.length > 2)
					ndBODY.removeChild(ndBODY.childNodes[2]);
				if(data.indexOf("-----BEGIN PRACTISES SIGNED MESSAGE-----") == 0) {
					var hdr = GetDBView().hdrForFirstSelectedMessage;
					practises.verify(practises.stripEmail(hdr.mime2DecodedAuthor), data);
				}
			}
			
			practises.deleteFile(outputPath);
		}

		practises.deleteFile(tmp_file.path);
		
		return returnVal;
	},
	
	deleteFile: function(path) {
		var file = Components.classes["@mozilla.org/file/local;1"].createInstance(Components.interfaces.nsILocalFile);
		file.initWithPath(path);
		file.remove(false);
	},
	
	getFilename: function(url) {
		return url.substring(url.lastIndexOf("\\") + 1, url.length);
	},

	prompt: function(title, message) {
		var prompts = Components.classes["@mozilla.org/embedcomp/prompt-service;1"].getService(Components.interfaces.nsIPromptService);
		var input = { value: "" };
		var check = { value: false };
		var dialogResult = prompts.promptPassword(window, title, message, input, null, check);

		if(!dialogResult || input.value == "") {
			return null;
		}

		return input.value;
	},
	
	readFile: function(path) {
		var file = Components.classes["@mozilla.org/file/local;1"].createInstance(Components.interfaces.nsILocalFile);
		var istream = Components.classes["@mozilla.org/network/file-input-stream;1"].createInstance(Components.interfaces.nsIFileInputStream);
		var is = Components.classes["@mozilla.org/intl/converter-input-stream;1"].createInstance(Components.interfaces.nsIConverterInputStream);

		file.initWithPath(path);
		istream.init(file, 0x01, 0, 0);
		istream.QueryInterface(Components.interfaces.nsIInputStream);
		is.init(istream, CHARSET, 1024, REPLACEMENTCHAR);

		var data = "";
		var str = {};

		while(is.readString(1024, str) != 0) {
			data += str.value;
		};

		is.close();
		istream.close();
		
		return data;
	},
	
	readMessage: function() {
		var offset = new Object();
		var messageSize = new Object();
		var is = null;
		
		var url = document.getElementById("messagepane").currentURI.spec;
		var hdr = GetDBView().hdrForFirstSelectedMessage;

		try {
			is = hdr.folder.getOfflineFileStream(hdr.messageKey, offset, messageSize);
		} catch(e) {
			alert("message: " + e.message);
		}
		
		try {
			var sis = Components.classes["@mozilla.org/scriptableinputstream;1"].createInstance(Components.interfaces.nsIScriptableInputStream);
			sis.init(is);
			bodyAndHdr = sis.read(hdr.messageSize);
		} catch(e) {
			alert("message: " + e.message);
		}
		
		var hdrstr = bodyAndHdr.indexOf("\r\n\r\n");
		
		var body = bodyAndHdr.substring(hdrstr + 4, bodyAndHdr.length);
		body = body.replace(/(\r\n|\r|\n)/g, '\n');
		
		var psesStart = body.indexOf("-----BEGIN PRACTISES");
		var psesEnd = body.indexOf("-----END PRACTISES");
		psesEnd = body.indexOf("\n", psesEnd);
		
		return body.substring(psesStart, psesEnd + 1);
	},
	
	run: function(args) {
		var clientpath	= this.prefs.getCharPref("clientpath");
		var clientFile = Components.classes["@mozilla.org/file/local;1"].createInstance(Components.interfaces.nsILocalFile);
		var clientProcess = Components.classes["@mozilla.org/process/util;1"].createInstance(Components.interfaces.nsIProcess);
		
		clientFile.initWithPath(clientpath);
		clientProcess.init(clientFile);
		clientProcess.run(true, args, args.length);
		
		return clientProcess.exitValue;
	},
	
	saveViewAttachments: function() {
		var tempFolder = Components.classes["@mozilla.org/file/directory_service;1"].getService(Components.interfaces.nsIProperties).get("TmpD", Components.interfaces.nsILocalFile);
		var files = Array();
		var count = 0;

		try	{
			for (index in currentAttachments) {
				// exclude all attachments already deleted
				var attachment = currentAttachments[index];
				if (attachment.contentType != 'text/x-moz-deleted') {
					files[count++] = messenger.saveAttachmentToFolder(attachment.contentType, attachment.url, encodeURI(attachment.displayName), attachment.uri, tempFolder);
				}
			}
		} catch(e) {
			dump ("** failed to handle all attachments **\n");
		}
		
		return files;
	},
	
	setVerifyIcon: function(status) {
		var psesBox = document.getElementById("psesBox");
		var icon = document.getElementById("psesVerifyIcon");

		if(status == 0) {
			icon.setAttribute("signed", "ok");
			icon.setAttribute("tooltiptext", this.strings.getString("statusOk"));
		} else if(status == 1) {
			icon.setAttribute("signed", "notok");
			icon.setAttribute("tooltiptext", this.strings.getString("statusNotok"));
		} else {
			icon.setAttribute("signed", "unknown");
			icon.setAttribute("tooltiptext", this.strings.getString("statusUnknown"));
		}

		psesBox.collapsed = false;
	},

	stripEmail: function(recipient) {
		var email = null;
		
		if(recipient.indexOf("<") == -1) {
			email = recipient;
		} else {
			var start = recipient.indexOf("<");
			var end = recipient.indexOf(">");
			email = recipient.substring(start + 1, end);
		}
		
		return email;
	},
	
	URItoWindows: function(uri) {
		uri = decodeURI(uri);
		uri = uri.replace(/file:\/\/\//, "");
		uri = uri.replace(/\//g, "\\");
		return uri;
	}
};

window.addEventListener("load", function(e) { practises.onLoad(e); }, false);
