import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input, OnInit, ViewChild } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { TimeagoModule } from 'ngx-timeago';
import { MessageService } from 'src/app/_services/message.service';
import * as forge from 'node-forge';
import {keys} from 'src/app/Keys/keys';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-member-messages',
  standalone: true,
  templateUrl: './member-messages.component.html',
  styleUrls: ['./member-messages.component.css'],
  imports: [CommonModule, TimeagoModule, FormsModule]
})

export class MemberMessagesComponent implements OnInit {
  @ViewChild('messageForm') messageForm?: NgForm;
  @Input() username?: string;
  messageContent = '';
  loading = false;

  publicKey: string = keys.publicKey;
  privateKey: string = keys.privateKey;

  constructor(public messageService: MessageService) { }

  ngOnInit(): void {
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

  sendMessage() {
    var rsa = forge.pki.publicKeyFromPem(this.publicKey);
    var encryptedMessage = window.btoa(rsa.encrypt( this.messageContent));

    if (!this.username) return;
    this.loading = true;
    this.messageService.sendMessage(this.username, encryptedMessage).then(() => {
      this.messageForm?.reset();
    }).finally(() => this.loading = false);
  }

}
