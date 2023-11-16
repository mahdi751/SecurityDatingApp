import { Component } from '@angular/core';
import { Message } from '../_models/message';
import { Pagination } from '../_models/pagination';
import { MessageService } from '../_services/message.service';
import {keys} from 'src/app/Keys/keys';
import * as forge from 'node-forge';

@Component({
  selector: 'app-messages',
  templateUrl: './messages.component.html',
  styleUrls: ['./messages.component.css']
})
export class MessagesComponent {
  messages?: Message[] = [];
  pagination?: Pagination;
  container = "Unread";
  pageNumber = 1;
  pageSize = 5;
  loading = false;

  publicKey: string = keys.publicKey;
  privateKey: string = keys.privateKey;

  constructor(private messageService: MessageService) { }

  ngOnInit(): void {
    this.loadMessages();
  }

  loadMessages() {
    this.loading = true;
    this.messageService.getMessages(this.pageNumber, this.pageSize, this.container).subscribe({
      next: response => {
        this.messages = response.result;
        this.pagination = response.pagination;
        this.loading = false;
      }
    })
  }

  decryptMessage(encryptedMessage: string): string {
    try {
      var privateKey = forge.pki.privateKeyFromPem(this.privateKey);
      return privateKey.decrypt(window.atob(encryptedMessage));
    } catch (error) {
      console.error('Error decrypting message:', error);
      return 'Error: Unable to decrypt message.';
    }
  }

  deleteMessage(id: number) {
    this.messageService.deleteMessage(id).subscribe({
      next: _ => this.messages?.splice(this.messages.findIndex(m => m.id === id), 1)
    })
  }

  pageChanged(event: any) {
    if (this.pageNumber !== event.page) {
      this.pageNumber = event.page;
      this.loadMessages();
    }
  }
}
