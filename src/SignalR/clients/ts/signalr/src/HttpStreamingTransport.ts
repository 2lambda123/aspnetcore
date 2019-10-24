// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

import { AbortController } from "./AbortController";
import { HttpClient, HttpResponse } from "./HttpClient";
import { ILogger, LogLevel } from "./ILogger";
import { ITransport, TransferFormat } from "./ITransport";
import { getDataDetail, getUserAgentHeader } from "./Utils";

// Not exported from 'index', this type is internal.
/** @private */
export class HttpStreamingTransport implements ITransport {
    private readonly httpClient: HttpClient;
    // @ts-ignore
    private readonly accessTokenFactory: (() => string | Promise<string>) | undefined;
    private readonly logger: ILogger;
    private readonly logMessageContent: boolean;
    private streamPromise: Promise<any>;
    private url: string = "";
    private reader: ReadableStreamReader | null;
    private streamAbort: AbortController;

    public onreceive: ((data: string | ArrayBuffer) => void) | null;
    public onclose: ((error?: Error) => void) | null;

    constructor(httpClient: HttpClient, accessTokenFactory: (() => string | Promise<string>) | undefined, logger: ILogger, logMessageContent: boolean) {
        if (!httpClient.supportsStreaming) {
            throw new Error("Streaming not supported in this environment.");
        }
        this.httpClient = httpClient;
        this.accessTokenFactory = accessTokenFactory;
        this.logger = logger;
        this.logMessageContent = logMessageContent;

        this.onreceive = null;
        this.onclose = null;
        this.reader = null;
        this.streamPromise = Promise.resolve();
        this.streamAbort = new AbortController();
    }

    // @ts-ignore
    public async connect(url: string, transferFormat: TransferFormat): Promise<void> {
        this.url = url;

        const token = await this.getAccessToken();
        const headers: { [key: string]: string } = {};
        this.updateHeaderToken(headers, token);

        const [name, value] = getUserAgentHeader();
        headers[name] = value;

        const response = await this.httpClient.get(url, { abortSignal: this.streamAbort.signal, headers, stream: true });

        this.streamPromise = this.stream(response);
    }

    private async stream(response: HttpResponse): Promise<void> {
        if (response.content instanceof ReadableStream) {
            this.reader = response.content!.getReader();
            let first = false;
            while (true) {
                const result = await this.reader.read();
                if (result.done) {
                    break;
                }

                if (!first) {
                    first = true;
                    continue;
                }

                this.logger.log(LogLevel.Trace, `(HttpStreaming transport) data received. ${getDataDetail(result.value, this.logMessageContent)}.`);
                if (this.onreceive) {
                    this.onreceive((result.value as Uint8Array).buffer);
                }
            }
        }
    }

    public async send(data: any): Promise<void> {
        const token = await this.getAccessToken();
        const headers: { [key: string]: string } = {};
        this.updateHeaderToken(headers, token);

        const [name, value] = getUserAgentHeader();
        headers[name] = value;

        this.logger.log(LogLevel.Trace, `(HttpStreaming transport) sending data. ${getDataDetail(data, this.logMessageContent)}.`);

        await this.httpClient.post(this.url, { content: data, headers });
    }

    public async stop(): Promise<void> {
        if (this.reader) {
            await this.reader.cancel();
        }
        this.streamAbort.abort();
        await this.streamPromise;

        if (this.onclose) {
            this.onclose();
        }
    }

    private async getAccessToken(): Promise<string | null> {
        if (this.accessTokenFactory) {
            return await this.accessTokenFactory();
        }

        return null;
    }

    private updateHeaderToken(headers: { [key: string]: string }, token: string | null) {
        if (token) {
            // tslint:disable-next-line:no-string-literal
            headers["Authorization"] = `Bearer ${token}`;
            return;
        }
        // tslint:disable-next-line:no-string-literal
        if (headers["Authorization"]) {
            // tslint:disable-next-line:no-string-literal
            delete headers["Authorization"];
        }
    }
}
